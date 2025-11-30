using AutoFixture;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;
using Supplier.Ingestion.Orchestrator.Api.Shared;
using Testcontainers.Kafka;

namespace Supplier.Ingestion.Orchestrator.Tests.Integration;

public class SupplierAStateMachineTests : IAsyncLifetime
{
    private readonly IFixture _fixture = new Fixture();

    // Configura o Container do Kafka (Versão compatível com Confluent)
    private readonly KafkaContainer _kafkaContainer = new KafkaBuilder()
        .WithImage("confluentinc/cp-kafka:7.5.0")
        .Build();

    public async Task InitializeAsync()
    {
        // 1. Inicia o Container
        await _kafkaContainer.StartAsync();

        // 2. Cria os tópicos explicitamente antes do MassTransit rodar
        // Isso evita o erro "Unknown topic or partition"
        var config = new AdminClientConfig
        {
            BootstrapServers = _kafkaContainer.GetBootstrapAddress()
        };

        using var adminClient = new AdminClientBuilder(config).Build();

        try
        {
            await adminClient.CreateTopicsAsync(new[]
            {
                new TopicSpecification { Name = "source.fornecedor-a.v1", NumPartitions = 1, ReplicationFactor = 1 },
                new TopicSpecification { Name = "target.dados.processados.v1", NumPartitions = 1, ReplicationFactor = 1 },
                new TopicSpecification { Name = "target.dados.invalidos.v1", NumPartitions = 1, ReplicationFactor = 1 }
            });
        }
        catch (CreateTopicsException e)
        {
            // Ignora se já existir (embora no Testcontainers seja sempre novo)
            if (e.Results.Any(r => r.Error.Code != ErrorCode.TopicAlreadyExists))
            {
                throw;
            }
        }
    }
    public async Task DisposeAsync() => _kafkaContainer.DisposeAsync();

    [Fact]
    public async Task Deve_Processar_Validar_E_Finalizar_Com_Sucesso()
    {
        //Arrange
        var topicInput = "source.fornecedor-a.v1";
        var topicSuccess = "target.dados.processados.v1";
        var topicError = "target.dados.invalidos.v1";
        var consumerGroup = "saga-orchestrator-test-group";

        await using var provider = new ServiceCollection()
            .AddLogging(l => l.AddConsole()) // Ajuda a debugar
            .AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<SupplierAStateMachine, InfringementState>()
                 .InMemoryRepository();

                x.AddRider(rider =>
                {
                    rider.AddProducer<string, UnifiedInfringementProcessed>(topicSuccess);
                    rider.AddProducer<string, InfringementValidationFailed>(topicError);

                    rider.AddProducer<string, SupplierAInputReceived>(topicInput);

                    rider.UsingKafka((context, k) =>
                    {
                        k.Host(_kafkaContainer.GetBootstrapAddress());

                        k.TopicEndpoint<SupplierAInputReceived>(topicInput, consumerGroup, e =>
                        {
                            var stateMachine = context.GetRequiredService<SupplierAStateMachine>();
                            var repository = context.GetRequiredService<ISagaRepository<InfringementState>>();

                            e.StateMachineSaga(stateMachine, repository);

                            e.AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest;
                        });
                    });
                });
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        using var scope = provider.CreateScope();
        var producer = scope.ServiceProvider.GetRequiredService<ITopicProducer<string, SupplierAInputReceived>>();

        var inputMessage = _fixture.Build<SupplierAInputReceived>()
            .With(x => x.TotalValue, 150.00m) // Valor válido
            .Create();

        // Act
        await harness.Bus.Publish(inputMessage);

        var sagaHarness = harness.GetSagaStateMachineHarness<SupplierAStateMachine, InfringementState>();

        Assert.True(await sagaHarness.Consumed.Any<SupplierAInputReceived>());

        await DisposeAsync();
    }
}