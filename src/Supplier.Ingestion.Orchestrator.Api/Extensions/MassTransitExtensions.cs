using Confluent.Kafka;
using Confluent.Kafka.Admin;
using MassTransit;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Consumers;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;

namespace Supplier.Ingestion.Orchestrator.Api.Extensions;

public static class MassTransitExtensions
{
    public static IServiceCollection AddMassTransitExtensions(this IServiceCollection services, IConfiguration configuration)
    {
        var topicProcessed = configuration["Kafka:Topics:ProcessedOutput"] ?? "target.processed.data.v1";
        var topicInvalid = configuration["Kafka:Topics:InvalidOutput"] ?? "target.invalid.data.v1";
        var topicSupplierA = configuration["Kafka:Topics:SupplierAInput"] ?? "source.supplier-a.v1";
        var topicSupplierB = configuration["Kafka:Topics:SupplierBInput"] ?? "source.supplier-b.v1";
        var consumerGroupA = configuration["Kafka:ConsumerGroups:SupplierA"] ?? "saga-group-a";
        var consumerGroupB = configuration["Kafka:ConsumerGroups:SupplierB"] ?? "saga-group-b";
        var consumerGroupDlq = configuration["Kafka:ConsumerGroups:Dlq"] ?? "dlq-group";

        services.AddHostedService(_ => new KafkaOutputTopicInitializer(
            configuration.GetConnectionString("Kafka")!,
            topicProcessed,
            topicInvalid));

        services.AddMassTransit(x =>
        {
            GlobalTopology.Send.UseCorrelationId<SupplierAInputReceived>(msg => msg.CorrelationId);
            GlobalTopology.Send.UseCorrelationId<SupplierBInputReceived>(msg => msg.CorrelationId);

            x.AddSagaStateMachine<SupplierAStateMachine, SupplierState>();
            x.AddSagaStateMachine<SupplierBStateMachine, SupplierState>();

            x.AddSagaRepository<SupplierState>()
                .MongoDbRepository(r =>
                {
                    r.Connection = configuration.GetConnectionString("MongoDb");
                    r.DatabaseName = "IngestionRefineryDb";
                    r.CollectionName = "InfringementSagas";
                });

            x.AddConsumer<InvalidInfringementConsumer>();

            x.UsingInMemory((context, cfg) =>
            {
                cfg.ConfigureJsonSerializerOptions(options =>
                {
                    options.PropertyNameCaseInsensitive = true;
                    return options;
                });

                cfg.ConfigureEndpoints(context);
            });

            x.AddRider(rider =>
            {
                rider.AddProducer<string, UnifiedInfringementProcessed>(topicProcessed);
                rider.AddProducer<string, InfringementValidationFailed>(topicInvalid);
                rider.AddProducer<string, SupplierAInputReceived>(topicSupplierA);
                rider.AddProducer<string, SupplierBInputReceived>(topicSupplierB);

                rider.UsingKafka((context, k) =>
                {
                    k.Host(configuration.GetConnectionString("Kafka"));

                    k.TopicEndpoint<SupplierAInputReceived>(topicSupplierA, consumerGroupA, e =>
                    {
                        e.AutoOffsetReset = AutoOffsetReset.Earliest;
                        e.ConcurrentMessageLimit = 10;
                        e.UseMessageRetry(r => r.Exponential(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2)));
                        e.UseRawJsonSerializer();
                        e.CreateIfMissing(t => { t.NumPartitions = 2; t.ReplicationFactor = 1; });

                        var machine = context.GetRequiredService<SupplierAStateMachine>();
                        e.StateMachineSaga(machine, context);
                    });

                    k.TopicEndpoint<SupplierBInputReceived>(topicSupplierB, consumerGroupB, e =>
                    {
                        e.AutoOffsetReset = AutoOffsetReset.Earliest;
                        e.ConcurrentMessageLimit = 10;
                        e.UseMessageRetry(r => r.Exponential(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2)));
                        e.UseRawJsonSerializer();
                        e.CreateIfMissing(t => { t.NumPartitions = 2; t.ReplicationFactor = 1; });

                        var machine = context.GetRequiredService<SupplierBStateMachine>();
                        e.StateMachineSaga(machine, context);
                    });

                    k.TopicEndpoint<InfringementValidationFailed>(topicInvalid, consumerGroupDlq, e =>
                    {
                        e.AutoOffsetReset = AutoOffsetReset.Earliest;
                        e.UseRawJsonSerializer();
                        e.ConfigureConsumer<InvalidInfringementConsumer>(context);
                    });
                });
            });
        });

        return services;
    }
}

file sealed class KafkaOutputTopicInitializer(string bootstrapServers, params string[] topics) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrapServers }).Build();
        var specs = topics.Select(t => new TopicSpecification { Name = t, NumPartitions = 2, ReplicationFactor = 1 }).ToList();
        try { await admin.CreateTopicsAsync(specs); }
        catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists)) { }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
