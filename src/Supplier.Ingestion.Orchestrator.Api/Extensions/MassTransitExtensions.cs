using System.Reflection;
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
        var consumerGroupDlq = configuration["Kafka:ConsumerGroups:Dlq"] ?? "dlq-group";

        // Descobre todas as state machines concretas que herdam de SupplierStateMachineBase<TInputEvent>
        var supplierRegistrations = DiscoverSupplierRegistrations(configuration);

        services.AddHostedService(_ => new KafkaOutputTopicInitializer(
            configuration.GetConnectionString("Kafka")!,
            topicProcessed,
            topicInvalid));

        services.AddMassTransit(x =>
        {
            // Registra automaticamente todas as state machines encontradas no assembly
            x.AddSagaStateMachines(Assembly.GetExecutingAssembly());

            x.AddSagaRepository<SupplierState>()
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
                rider.AddConsumer<InvalidInfringementConsumer>();

                rider.AddProducer<string, UnifiedInfringementProcessed>(topicProcessed);
                rider.AddProducer<string, InfringementValidationFailed>(topicInvalid);

                // Registra producers de replay para cada supplier descoberto
                foreach (var reg in supplierRegistrations)
                    reg.AddReplayProducer(rider);

                rider.UsingKafka((context, k) =>
                {
                    k.Host(configuration.GetConnectionString("Kafka"));

                    // Configura dinamicamente um TopicEndpoint por state machine descoberta
                    foreach (var reg in supplierRegistrations)
                        reg.ConfigureTopicEndpoint(k, context);

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

    private static List<SupplierRegistration> DiscoverSupplierRegistrations(IConfiguration configuration)
    {
        var baseType = typeof(SupplierStateMachineBase<>);

        return Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => !t.IsAbstract
                        && t.BaseType?.IsGenericType == true
                        && t.BaseType.GetGenericTypeDefinition() == baseType)
            .Select(smType =>
            {
                var eventType = smType.BaseType!.GetGenericArguments()[0];
                var supplierName = smType.Name.Replace("StateMachine", ""); // e.g. "SupplierA"
                var topic = configuration[$"Kafka:Topics:{supplierName}Input"]
                            ?? $"source.{supplierName.ToLower()}.v1";
                var consumerGroup = configuration[$"Kafka:ConsumerGroups:{supplierName}"]
                                    ?? $"saga-group-{supplierName.ToLower()}";

                return new SupplierRegistration(smType, eventType, topic, consumerGroup);
            })
            .ToList();
    }

    private static void ConfigureSupplierTopicEndpoint<TEvent, TStateMachine>(
        IKafkaFactoryConfigurator kafka,
        IRiderRegistrationContext context,
        string topic,
        string consumerGroup)
        where TEvent : class, ISupplierInputEvent
        where TStateMachine : MassTransitStateMachine<SupplierState>
    {
        kafka.TopicEndpoint<TEvent>(topic, consumerGroup, e =>
        {
            e.AutoOffsetReset = AutoOffsetReset.Earliest;
            e.ConcurrentMessageLimit = 10;
            e.UseMessageRetry(r => r.Exponential(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2)));
            e.UseRawJsonSerializer();
            e.CreateIfMissing(t => { t.NumPartitions = 2; t.ReplicationFactor = 1; });
            e.StateMachineSaga(context.GetRequiredService<TStateMachine>(), context);
        });
    }

    private static void AddSupplierProducer<TEvent>(IRiderRegistrationConfigurator rider, string topic)
        where TEvent : class
        => rider.AddProducer<string, TEvent>(topic);

    private static readonly MethodInfo ConfigureEndpointMethod =
        typeof(MassTransitExtensions)
            .GetMethod(nameof(ConfigureSupplierTopicEndpoint), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo AddProducerMethod =
        typeof(MassTransitExtensions)
            .GetMethod(nameof(AddSupplierProducer), BindingFlags.NonPublic | BindingFlags.Static)!;

    private sealed record SupplierRegistration(
        Type StateMachineType,
        Type EventType,
        string Topic,
        string ConsumerGroup)
    {
        public void AddReplayProducer(IRiderRegistrationConfigurator rider)
            => AddProducerMethod
                .MakeGenericMethod(EventType)
                .Invoke(null, [rider, Topic]);

        public void ConfigureTopicEndpoint(IKafkaFactoryConfigurator kafka, IRiderRegistrationContext context)
            => ConfigureEndpointMethod
                .MakeGenericMethod(EventType, StateMachineType)
                .Invoke(null, [kafka, context, Topic, ConsumerGroup]);
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
