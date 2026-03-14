using Anthropic;
using Supplier.Ingestion.Orchestrator.Api.Extensions;
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

builder.Services.AddMassTransitExtensions(builder.Configuration);
builder.Services.AddHealthCheckExtensions(builder.Configuration);

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseApplicationExtensions();

app.Run();
