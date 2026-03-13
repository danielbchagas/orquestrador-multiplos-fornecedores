using Anthropic;
using Supplier.Ingestion.Orchestrator.Api.Extensions;
using Supplier.Ingestion.Orchestrator.Api.Validators;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddMassTransitExtensions(builder.Configuration);
builder.Services.AddHealthCheckExtensions(builder.Configuration);
builder.Services.AddOpenTelemetryExtensions(builder.Configuration);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddOpenTelemetryLogging(builder.Configuration);

var app = builder.Build();

app.UseApplicationExtensions();

app.Run();