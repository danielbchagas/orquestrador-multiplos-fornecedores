using AutoFixture;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;
using Testcontainers.Kafka;

namespace Supplier.Ingestion.Orchestrator.Tests.IntegrationTests.Infrastructure.StateMachines;

public class SupplierBStateMachineTests : IAsyncLifetime
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
                new TopicSpecification { Name = "integration-source.fornecedor-b.v1", NumPartitions = 1, ReplicationFactor = 1 },
                new TopicSpecification { Name = "integration-target.dados.processados.v1", NumPartitions = 1, ReplicationFactor = 1 },
                new TopicSpecification { Name = "integration-target.dados.invalidos.v1", NumPartitions = 1, ReplicationFactor = 1 }
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
    public async Task DisposeAsync() => await _kafkaContainer.DisposeAsync();

    [Fact]
    public async Task Deve_Processar_Validar_E_Finalizar_Com_Sucesso()
    {
        //Arrange
        var topicInput = "integration-source.fornecedor-b.v1";
        var topicSuccess = "integration-target.dados.processados.v1";
        var topicError = "integration-target.dados.invalidos.v1";
        var consumerGroup = "integration-saga-orchestrator-test-group";

        await using var provider = new ServiceCollection()
            .AddLogging(l => l.AddConsole()) // Ajuda a debugar
            .AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<SupplierBStateMachine, SupplierState>()
                 .InMemoryRepository();

                x.AddRider(rider =>
                {
                    rider.AddProducer<string, UnifiedInfringementProcessed>(topicSuccess);
                    rider.AddProducer<string, InfringementValidationFailed>(topicError);

                    rider.AddProducer<string, SupplierBInputReceived>(topicInput);

                    rider.UsingKafka((context, k) =>
                    {
                        k.Host(_kafkaContainer.GetBootstrapAddress());

                        k.TopicEndpoint<SupplierBInputReceived>(topicInput, consumerGroup, e =>
                        {
                            var stateMachine = context.GetRequiredService<SupplierBStateMachine>();
                            var repository = context.GetRequiredService<ISagaRepository<SupplierState>>();

                            e.StateMachineSaga(stateMachine, repository);

                            e.AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest;
                        });
                    });
                });
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var inputMessage = _fixture.Build<SupplierBInputReceived>()
            .With(x => x.TotalValue, 150.00m) // Valor válido
            .Create();

        // Act
        await harness.Bus.Publish(inputMessage);

        var sagaHarness = harness.GetSagaStateMachineHarness<SupplierBStateMachine, SupplierState>();
        var message = sagaHarness.Sagas.Contains(inputMessage.CorrelationId);

        // Assert
        Assert.True(await sagaHarness.Consumed.Any<SupplierBInputReceived>());

        message.Should().NotBeNull("A saga deve existir com o CorrelationId fornecido.");
        message.CorrelationId.Should().Be(inputMessage.CorrelationId, "O CorrelationId da saga deve corresponder ao do evento publicado.");
        message.ExternalId.Should().Be(inputMessage.ExternalCode, "O ExternalCode deve ser copiado corretamente do evento para o estado da saga.");
        message.Plate.Should().Be(inputMessage.Plate, "A placa deve ser copiada corretamente do evento para o estado da saga.");
        message.InfringementCode.Should().Be(inputMessage.Infringement, "O código de infração deve ser copiado corretamente do evento para o estado da saga.");
        message.Amount.Should().Be(inputMessage.TotalValue, "O valor total deve ser copiado corretamente do evento para o estado da saga.");
        message.OriginSystem.Should().Be(inputMessage.OriginSystem, "O Sistema de origem deve ser copiado corretamente do evento para o estado da saga.");
    }
}
