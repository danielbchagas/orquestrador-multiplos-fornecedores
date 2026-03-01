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
            // Erro 1 corrigido: state machines com tipos de estado separados — sem colisão no DI
            x.AddSagaStateMachine<SupplierAStateMachine, SupplierAState>();
            x.AddSagaStateMachine<SupplierBStateMachine, SupplierBState>();

            // Erro 1 corrigido: repositórios separados com coleções MongoDB distintas
            x.AddSagaRepository<SupplierAState>()
                .MongoDbRepository(r =>
                {
                    r.Connection = configuration.GetConnectionString("MongoDb");
                    r.DatabaseName = "IngestionRefineryDb";
                    r.CollectionName = "SupplierASagas";
                });

            x.AddSagaRepository<SupplierBState>()
                .MongoDbRepository(r =>
                {
                    r.Connection = configuration.GetConnectionString("MongoDb");
                    r.DatabaseName = "IngestionRefineryDb";
                    r.CollectionName = "SupplierBSagas";
                });

            // Erro 4 corrigido: ConfigureEndpoints removido — sagas são consumidas exclusivamente
            // via Kafka TopicEndpoints; criava endpoints fantasmas no bus InMemory
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

                    // Erro 3 corrigido: ConcurrentMessageLimit adicionado para consistência com SupplierA
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
