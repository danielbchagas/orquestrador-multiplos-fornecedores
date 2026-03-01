using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Supplier.Ingestion.Orchestrator.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddMassTransitExtensions(builder.Configuration);
builder.Services.AddHealthCheckExtensions(builder.Configuration);

var otlpEndpoint = builder.Configuration["Otlp:Endpoint"];

if (string.IsNullOrEmpty(otlpEndpoint))
    throw new InvalidOperationException("Otlp:Endpoint configuration is missing.");

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("Supplier.Ingestion.Orchestrator"))
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter("MassTransit")
            .AddOtlpExporter(opts =>
            {
                opts.Endpoint = new Uri(otlpEndpoint);
            });
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("MassTransit")
            .AddOtlpExporter(opts =>
            {
                opts.Endpoint = new Uri(otlpEndpoint);
            });
    });

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeScopes = true;
    logging.IncludeFormattedMessage = true;
    logging.SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService("Supplier.Ingestion.Orchestrator"));

    logging.AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri(otlpEndpoint);
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Scalar Example API")
            .WithTheme(ScalarTheme.Mars)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapGet("/", () => "Supplier Ingestion Orchestrator API is running...");

app.MapGet("/grafana", (ILogger<Program> logger) =>
{
    logger.LogInformation("Teste de Observabilidade! {Time}", DateTime.UtcNow);
    return "Dados enviados para o Grafana!";
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();