using MassTransit;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;
using Supplier.Ingestion.Orchestrator.Api.Shared;

namespace Supplier.Ingestion.Orchestrator.Api.Extensions;

public static class MassTransitSetup
{
    public static IServiceCollection AddCustomMassTransit(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMassTransit(x =>
        {
            GlobalTopology.Send.UseCorrelationId<SupplierAInputReceived>(msg => msg.CorrelationId);
            GlobalTopology.Send.UseCorrelationId<SupplierBInputReceived>(msg => msg.CorrelationId);

            x.AddSagaStateMachine<SupplierAStateMachine, InfringementState>();
            x.AddSagaStateMachine<SupplierBStateMachine, InfringementState>();

            x.AddSagaRepository<InfringementState>()
                .MongoDbRepository(r =>
                {
                    r.Connection = configuration.GetConnectionString("MongoDb");
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
                rider.AddProducer<string, UnifiedInfringementProcessed>("target.dados.processados.v1");
                rider.AddProducer<string, InfringementValidationFailed>("target.dados.invalidos.v1");

                rider.UsingKafka((context, k) =>
                {
                    k.Host(configuration.GetConnectionString("Kafka"));

                    k.TopicEndpoint<SupplierAInputReceived>("source.fornecedor-a.v1", "saga-group-a", e =>
                    {
                        e.AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest;
                        e.ConcurrentMessageLimit = 10;

                        e.UseRawJsonSerializer();

                        var machine = context.GetRequiredService<SupplierAStateMachine>();
                        e.StateMachineSaga(machine, context);
                    });

                    k.TopicEndpoint<SupplierBInputReceived>("source.fornecedor-b.v1", "saga-group-b", e =>
                    {
                        e.AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest;

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