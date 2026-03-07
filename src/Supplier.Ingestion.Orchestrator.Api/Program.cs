using Supplier.Ingestion.Orchestrator.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddMassTransitExtensions(builder.Configuration);
builder.Services.AddHealthCheckExtensions(builder.Configuration);
builder.Services.AddOpenTelemetryExtensions(builder.Configuration);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddOpenTelemetryLogging(builder.Configuration);

var app = builder.Build();

app.UseApplicationExtensions();

app.Run();