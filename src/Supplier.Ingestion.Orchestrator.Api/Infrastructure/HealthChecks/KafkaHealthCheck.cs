using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Supplier.Ingestion.Orchestrator.Api.Infrastructure.HealthChecks;

public class KafkaHealthCheck(Func<IAdminClient> adminClientFactory) : IHealthCheck
{
    private static readonly TimeSpan MetadataTimeout = TimeSpan.FromSeconds(5);

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var adminClient = adminClientFactory();
            var metadata = adminClient.GetMetadata(MetadataTimeout);

            if (metadata?.Brokers?.Count > 0)
                return Task.FromResult(
                    HealthCheckResult.Healthy($"Kafka is reachable. Brokers: {metadata.Brokers.Count}"));

            return Task.FromResult(
                HealthCheckResult.Degraded("Kafka returned no broker metadata."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy("Kafka is unreachable.", ex));
        }
    }
}
