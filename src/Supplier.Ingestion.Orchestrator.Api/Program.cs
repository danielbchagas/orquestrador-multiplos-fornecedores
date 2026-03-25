using Anthropic;
using MassTransit;
using Microsoft.AspNetCore.RateLimiting;
using MongoDB.Driver;
using Supplier.Ingestion.Orchestrator.Api.Extensions;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Repositories;
using Supplier.Ingestion.Orchestrator.Api.Security;
using Supplier.Ingestion.Orchestrator.Api.Validators;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("default", cfg =>
    {
        cfg.PermitLimit = 60;
        cfg.Window = TimeSpan.FromMinutes(1);
        cfg.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        cfg.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("strict", cfg =>
    {
        cfg.PermitLimit = 10;
        cfg.Window = TimeSpan.FromMinutes(1);
        cfg.QueueLimit = 0;
    });

    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync("Rate limit excedido.", ct);
    };
});

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var apiKey = config["Anthropic:ApiKey"];
    return string.IsNullOrEmpty(apiKey)
        ? new AnthropicClient()
        : new AnthropicClient { ApiKey = apiKey };
});
builder.Services.AddSingleton<AiInfringementValidator>();
builder.Services.AddSingleton<IAiInfringementValidator, ResilientAiInfringementValidator>();
builder.Services.AddSingleton<IInfringementValidator, InfringementValidator>();

builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(builder.Configuration.GetConnectionString("MongoDb")));
builder.Services.AddScoped<IInvalidInfringementRepository, InvalidInfringementRepository>();

builder.Services.AddScoped(typeof(KafkaSignatureVerificationFilter<>));
builder.Services.AddMassTransitExtensions(builder.Configuration);
builder.Services.AddHealthCheckExtensions(builder.Configuration);

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseApplicationExtensions();
app.UseRateLimiter();
app.UseMiddleware<AuditMiddleware>();

app.MapGet("/dlq", async (IInvalidInfringementRepository repo, CancellationToken ct) =>
    Results.Ok(await repo.GetAllAsync(50, ct)))
    .WithTags("DLQ")
    .RequireRateLimiting("default");

app.MapPost("/dlq/{id:guid}/retry", async (
    Guid id,
    IInvalidInfringementRepository repo,
    ITopicProducer<string, SupplierAInputReceived> producerA,
    ITopicProducer<string, SupplierBInputReceived> producerB,
    CancellationToken ct) =>
{
    var item = await repo.GetByIdAsync(id, ct);
    if (item is null) return Results.NotFound();

    await repo.IncrementRetryAsync(id, ct);

    if (item.OriginSystem == "SupplierA")
        await producerA.Produce(item.OriginId,
            new SupplierAInputReceived(item.OriginId, item.Plate, item.InfringementCode, item.Amount), ct);
    else
        await producerB.Produce(item.OriginId,
            new SupplierBInputReceived(item.OriginId, item.Plate, item.InfringementCode, item.Amount), ct);

    await repo.DeleteSagaAsync(item.CorrelationId, ct);

    return Results.Accepted();
}).WithTags("DLQ")
  .RequireRateLimiting("strict");

app.Run();
