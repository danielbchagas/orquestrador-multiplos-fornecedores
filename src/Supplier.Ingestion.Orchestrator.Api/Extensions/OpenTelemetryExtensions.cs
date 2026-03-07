using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Supplier.Ingestion.Orchestrator.Api.Extensions;

public static class OpenTelemetryExtensions
{
    private const string ServiceName = "Supplier.Ingestion.Orchestrator";

    public static IServiceCollection AddOpenTelemetryExtensions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var otlpEndpoint = configuration["Otlp:Endpoint"]
            ?? throw new InvalidOperationException("Otlp:Endpoint configuration is missing.");

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(ServiceName))
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

        return services;
    }

    public static ILoggingBuilder AddOpenTelemetryLogging(
        this ILoggingBuilder logging,
        IConfiguration configuration)
    {
        var otlpEndpoint = configuration["Otlp:Endpoint"]
            ?? throw new InvalidOperationException("Otlp:Endpoint configuration is missing.");

        logging.AddOpenTelemetry(options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
            options.SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService(ServiceName));

            options.AddOtlpExporter(opts =>
            {
                opts.Endpoint = new Uri(otlpEndpoint);
            });
        });

        return logging;
    }
}
