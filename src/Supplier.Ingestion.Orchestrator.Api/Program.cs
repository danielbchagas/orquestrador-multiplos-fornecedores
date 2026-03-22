using Anthropic;
using MassTransit;
using MongoDB.Driver;
using Supplier.Ingestion.Orchestrator.Api.Extensions;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Repositories;
using Supplier.Ingestion.Orchestrator.Api.Validators;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

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

builder.Services.AddMassTransitExtensions(builder.Configuration);
builder.Services.AddHealthCheckExtensions(builder.Configuration);

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseApplicationExtensions();

app.MapGet("/dlq", async (IInvalidInfringementRepository repo, CancellationToken ct) =>
    Results.Ok(await repo.GetAllAsync(50, ct)))
    .WithTags("DLQ");

app.MapPost("/dlq/{id:guid}/retry", async (
    Guid id,
    IInvalidInfringementRepository repo,
    ITopicProducer<string, SupplierAInputReceived> producerA,
    ITopicProducer<string, SupplierBInputReceived> producerB,
    CancellationToken ct) =>
{
    var item = await repo.GetByIdAsync(id, ct);
    if (item is null) return Results.NotFound();

    await repo.DeleteSagaAsync(item.CorrelationId, ct);

    if (item.OriginSystem == "SupplierA")
        await producerA.Produce(item.OriginId,
            new SupplierAInputReceived(item.OriginId, item.Plate, item.InfringementCode, item.Amount), ct);
    else
        await producerB.Produce(item.OriginId,
            new SupplierBInputReceived(item.OriginId, item.Plate, item.InfringementCode, item.Amount), ct);

    await repo.IncrementRetryAsync(id, ct);
    return Results.Accepted();
}).WithTags("DLQ");

app.Run();
