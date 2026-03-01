using MassTransit;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;

namespace Supplier.Ingestion.Orchestrator.Api.Extensions;

public static class MassTransitExtensions
{
    public static IServiceCollection AddMassTransitExtensions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMassTransit(x =>
        {
            x.AddSagaStateMachine<SupplierAStateMachine, SupplierAState>()
                .MongoDbRepository(r =>
                {
                    r.Connection = configuration.GetConnectionString("MongoDb");
                    r.DatabaseName = "IngestionRefineryDb";
                    r.CollectionName = "SupplierASagas";
                });

            x.AddSagaStateMachine<SupplierBStateMachine, SupplierBState>()
                .MongoDbRepository(r =>
                {
                    r.Connection = configuration.GetConnectionString("MongoDb");
                    r.DatabaseName = "IngestionRefineryDb";
                    r.CollectionName = "SupplierBSagas";
                });

            x.UsingInMemory((context, cfg) =>
            {
                cfg.ConfigureJsonSerializerOptions(options =>
                {
                    options.PropertyNameCaseInsensitive = true;
                    return options;
                });
            });

            x.AddRider(rider =>
            {
                rider.AddProducer<string, UnifiedInfringementProcessed>("target.processed.data.v1");
                rider.AddProducer<string, InfringementValidationFailed>("target.invalid.data.v1");

                rider.UsingKafka((context, k) =>
                {
                    k.Host(configuration.GetConnectionString("Kafka"));

                    k.TopicEndpoint<SupplierAInputReceived>("source.supplier-a.v1", "saga-group-a", e =>
                    {
                        e.AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest;
                        e.ConcurrentMessageLimit = 10;

                        e.UseRawJsonSerializer();

                        var machine = context.GetRequiredService<SupplierAStateMachine>();
                        e.StateMachineSaga(machine, context);
                    });

                    k.TopicEndpoint<SupplierBInputReceived>("source.supplier-b.v1", "saga-group-b", e =>
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
