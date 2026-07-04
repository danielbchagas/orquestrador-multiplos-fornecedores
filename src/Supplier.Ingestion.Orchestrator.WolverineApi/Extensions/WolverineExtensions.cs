using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.WolverineApi.Security;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Kafka;

namespace Supplier.Ingestion.Orchestrator.WolverineApi.Extensions;

public static class WolverineExtensions
{
    public static WebApplicationBuilder AddWolverineExtensions(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;

        var kafkaBootstrapServers = configuration.GetConnectionString("Kafka")
            ?? throw new InvalidOperationException("Kafka connection string is missing.");

        var topicProcessed = configuration["Kafka:Topics:ProcessedOutput"] ?? "target.processed.data.v1";
        var topicInvalid = configuration["Kafka:Topics:InvalidOutput"] ?? "target.invalid.data.v1";
        var consumerGroupDlq = configuration["Kafka:ConsumerGroups:Dlq"] ?? "wolverine-dlq-group";

        // Descobre todos os eventos de entrada de fornecedores (mesma convenção da versão MassTransit,
        // que descobre state machines — aqui descobrimos os eventos que implementam ISupplierInputEvent)
        var supplierRegistrations = DiscoverSupplierRegistrations(configuration);

        builder.UseWolverine(opts =>
        {
            opts.ServiceName = "supplier-ingestion-orchestrator-wolverine";

            // Mesmo contrato dos producers e da versão MassTransit: JSON camelCase, case-insensitive na leitura.
            // AllowReadingFromString é necessário porque o serializer raw JSON do MassTransit
            // escreve decimais como string (ex.: "amount": "195.23") no tópico de inválidas.
            opts.UseSystemTextJsonForSerialization(json =>
            {
                json.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                json.PropertyNameCaseInsensitive = true;
                json.NumberHandling = JsonNumberHandling.AllowReadingFromString;
            });

            opts.UseKafka(kafkaBootstrapServers).AutoProvision();

            foreach (var registration in supplierRegistrations)
            {
                // Consome o tópico de entrada do fornecedor. Os producers não são aplicações Wolverine,
                // então o tipo da mensagem é fixado por tópico via DefaultIncomingMessage.
                opts.ListenToKafkaTopic(registration.Topic)
                    .DefaultIncomingMessage(registration.EventType)
                    .ConfigureConsumer(consumer =>
                    {
                        consumer.GroupId = registration.ConsumerGroup;
                        consumer.AutoOffsetReset = AutoOffsetReset.Earliest;
                    })
                    .Specification(topic =>
                    {
                        topic.NumPartitions = 2;
                        topic.ReplicationFactor = 1;
                    });

                // Rota de replay: o retry manual da DLQ publica o evento de volta ao tópico de origem
                opts.PublishMessage(registration.EventType).ToKafkaTopic(registration.Topic);
            }

            opts.PublishMessage(typeof(UnifiedInfringementProcessed))
                .ToKafkaTopic(topicProcessed);

            opts.PublishMessage(typeof(InfringementValidationFailed))
                .ToKafkaTopic(topicInvalid);

            // Consome o próprio tópico de inválidas para persistir a DLQ no MongoDB
            opts.ListenToKafkaTopic(topicInvalid)
                .DefaultIncomingMessage<InfringementValidationFailed>()
                .ConfigureConsumer(consumer =>
                {
                    consumer.GroupId = consumerGroupDlq;
                    consumer.AutoOffsetReset = AutoOffsetReset.Earliest;
                })
                .Specification(topic =>
                {
                    topic.NumPartitions = 2;
                    topic.ReplicationFactor = 1;
                });

            // Equivalente ao UseMessageRetry(r => r.Exponential(3, ...)) da versão MassTransit
            opts.OnException<Exception>()
                .RetryWithCooldown(
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(4));

            // Equivalente ao UseConsumeFilter(KafkaSignatureVerificationFilter) da versão MassTransit,
            // aplicado apenas aos handlers dos eventos de entrada dos fornecedores
            opts.Policies.AddMiddleware(
                typeof(KafkaSignatureVerificationMiddleware),
                chain => typeof(ISupplierInputEvent).IsAssignableFrom(chain.MessageType));
        });

        return builder;
    }

    private static List<SupplierRegistration> DiscoverSupplierRegistrations(IConfiguration configuration)
    {
        return Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(ISupplierInputEvent).IsAssignableFrom(t))
            .Select(eventType =>
            {
                var supplierName = eventType.Name.Replace("InputReceived", ""); // e.g. "SupplierA"
                var topic = configuration[$"Kafka:Topics:{supplierName}Input"]
                            ?? $"source.{supplierName.ToLower()}.v1";
                var consumerGroup = configuration[$"Kafka:ConsumerGroups:{supplierName}"]
                                    ?? $"wolverine-group-{supplierName.ToLower()}";

                return new SupplierRegistration(eventType, topic, consumerGroup);
            })
            .ToList();
    }

    private sealed record SupplierRegistration(
        Type EventType,
        string Topic,
        string ConsumerGroup);
}
