using MassTransit;
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

        services.AddMassTransit(x =>
        {
            GlobalTopology.Send.UseCorrelationId<SupplierAInputReceived>(msg => msg.CorrelationId);
            GlobalTopology.Send.UseCorrelationId<SupplierBInputReceived>(msg => msg.CorrelationId);

            x.AddSagaStateMachine<SupplierAStateMachine, SupplierState>();
            x.AddSagaStateMachine<SupplierBStateMachine, SupplierState>();

            x.AddSagaRepository<SupplierState>()
                .MongoDbRepository(r =>
                {
                    r.Connection = configuration.GetConnectionString("IngestionRefineryDb")
                        ?? configuration.GetConnectionString("MongoDb");
                    r.DatabaseName = "IngestionRefineryDb";
                    r.CollectionName = "InfringementSagas";
                });

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

                rider.UsingKafka((context, k) =>
                {
                    k.Host(configuration.GetConnectionString("kafka")
                        ?? configuration.GetConnectionString("Kafka"));

                    k.TopicEndpoint<SupplierAInputReceived>(topicSupplierA, consumerGroupA, e =>
                    {
                        e.AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest;
                        e.ConcurrentMessageLimit = 10;

                        e.UseRawJsonSerializer();

                        var machine = context.GetRequiredService<SupplierAStateMachine>();
                        e.StateMachineSaga(machine, context);
                    });

                    k.TopicEndpoint<SupplierBInputReceived>(topicSupplierB, consumerGroupB, e =>
                    {
                        e.AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest;
                        e.ConcurrentMessageLimit = 10;

                        e.UseRawJsonSerializer();

                        var machine = context.GetRequiredService<SupplierBStateMachine>();
                        e.StateMachineSaga(machine, context);
                    });
                });
            });
        });

        return services;
    }
}
