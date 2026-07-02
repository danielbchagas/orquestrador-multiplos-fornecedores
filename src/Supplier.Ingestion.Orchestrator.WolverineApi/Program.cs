using Anthropic;
using JasperFx;
using Microsoft.AspNetCore.RateLimiting;
using MongoDB.Driver;
using Supplier.Ingestion.Orchestrator.WolverineApi.Extensions;
using Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Handlers;
using Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Repositories;
using Supplier.Ingestion.Orchestrator.WolverineApi.Security;
using Supplier.Ingestion.Orchestrator.WolverineApi.Validators;
using System.Threading.RateLimiting;
using Wolverine;

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
builder.Services.AddScoped<ISupplierIngestionStateRepository, SupplierIngestionStateRepository>();
builder.Services.AddScoped<SupplierInputProcessor>();

builder.AddWolverineExtensions();
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
    ISupplierIngestionStateRepository stateRepo,
    IMessageBus bus,
    CancellationToken ct) =>
{
    var item = await repo.GetByIdAsync(id, ct);
    if (item is null) return Results.NotFound();

    await repo.IncrementRetryAsync(id, ct);

    // Remove o estado antes do replay para liberar a barreira de idempotência
    await stateRepo.DeleteAsync(item.CorrelationId, ct);

    var delivery = new DeliveryOptions { PartitionKey = item.OriginId };

    if (item.OriginSystem == "SupplierA")
        await bus.PublishAsync(
            SupplierAInputReceived.Create(item.OriginId, item.Plate, item.InfringementCode, item.Amount),
            delivery);
    else
        await bus.PublishAsync(
            SupplierBInputReceived.Create(item.OriginId, item.Plate, item.InfringementCode, item.Amount),
            delivery);

    return Results.Accepted();
}).WithTags("DLQ")
  .RequireRateLimiting("strict");

// Habilita os comandos de diagnóstico do Wolverine/JasperFx (ex.: `dotnet run -- codegen preview`,
// `dotnet run -- describe`) e executa a aplicação normalmente quando não há comando nos args.
return await app.RunJasperFxCommands(args);
