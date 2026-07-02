using Confluent.Kafka;
using MongoDB.Driver;
using Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.HealthChecks;

namespace Supplier.Ingestion.Orchestrator.WolverineApi.Extensions;

public static class HealthCheckExtensions
{
    public static IServiceCollection AddHealthCheckExtensions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var kafkaBootstrapServers = configuration.GetConnectionString("Kafka")
            ?? throw new InvalidOperationException("Kafka connection string is missing.");

        services.AddHealthChecks()
            .AddCheck<MongoDbHealthCheck>(
                "mongodb",
                tags: ["ready", "db"])
            .AddCheck(
                "kafka",
                new KafkaHealthCheck(() => new AdminClientBuilder(new AdminClientConfig
                {
                    BootstrapServers = kafkaBootstrapServers,
                    SocketTimeoutMs = 5000
                }).Build()),
                tags: ["ready", "messaging"]);

        return services;
    }
}
