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

public abstract class SupplierStateMachineIntegrationTestsBase<TStateMachine, TInputEvent> : IAsyncLifetime
    where TStateMachine : class, MassTransit.SagaStateMachine<SupplierState>
    where TInputEvent : class, MassTransit.CorrelatedBy<Guid>
{
    protected readonly IFixture Fixture = new Fixture();

    private readonly KafkaContainer _kafkaContainer = new KafkaBuilder()
        .WithImage("confluentinc/cp-kafka:7.5.0")
        .Build();

    protected abstract string InputTopic    { get; }
    protected abstract string ConsumerGroup { get; }

    private const string TopicSuccess = "integration-target.processed.data.v1";
    private const string TopicError   = "integration-target.invalid.data.v1";

    /// <summary>
    /// Constrói um evento de entrada válido para ser publicado no teste.
    /// Os campos ExternalCode, Plate, Infringement, TotalValue e OriginSystem
    /// são usados nos asserts — certifique-se de preenchê-los corretamente.
    /// </summary>
    protected abstract TInputEvent BuildValidInputEvent();

    /// <summary>
    /// Registra o TopicEndpoint específico de cada fornecedor no Kafka rider.
    /// </summary>
    protected abstract void RegisterTopicEndpoint(
        IRiderRegistrationContext context,
        IKafkaFactoryConfigurator k);

    /// <summary>
    /// Projeta os campos do evento de entrada para os campos esperados na saga,
    /// permitindo que os asserts da classe base comparem os valores corretamente.
    /// </summary>
    protected abstract (string ExternalId, string Plate, int InfringementCode, decimal Amount, string OriginSystem)
        ProjectSagaFields(TInputEvent inputEvent);

    public async Task InitializeAsync()
    {
        await _kafkaContainer.StartAsync();

        using var adminClient = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = _kafkaContainer.GetBootstrapAddress()
        }).Build();

        try
        {
            await adminClient.CreateTopicsAsync(new[]
            {
                new TopicSpecification { Name = InputTopic,   NumPartitions = 1, ReplicationFactor = 1 },
                new TopicSpecification { Name = TopicSuccess, NumPartitions = 1, ReplicationFactor = 1 },
                new TopicSpecification { Name = TopicError,   NumPartitions = 1, ReplicationFactor = 1 }
            });
        }
        catch (CreateTopicsException e)
        {
            if (e.Results.Any(r => r.Error.Code != ErrorCode.TopicAlreadyExists))
                throw;
        }
    }

    public async Task DisposeAsync() => await _kafkaContainer.DisposeAsync();

    [Fact]
    public async Task Should_Process_Validate_And_Complete_Successfully()
    {
        // Arrange
        await using var provider = new ServiceCollection()
            .AddLogging(l => l.AddConsole())
            .AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<TStateMachine, SupplierState>()
                 .InMemoryRepository();

                x.AddRider(rider =>
                {
                    rider.AddProducer<string, UnifiedInfringementProcessed>(TopicSuccess);
                    rider.AddProducer<string, InfringementValidationFailed>(TopicError);
                    rider.AddProducer<string, TInputEvent>(InputTopic);

                    rider.UsingKafka((context, k) =>
                    {
                        k.Host(_kafkaContainer.GetBootstrapAddress());
                        RegisterTopicEndpoint(context, k);
                    });
                });
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var inputMessage = BuildValidInputEvent();
        var expected = ProjectSagaFields(inputMessage);

        // Act
        await harness.Bus.Publish(inputMessage);

        var sagaHarness = harness.GetSagaStateMachineHarness<TStateMachine, SupplierState>();
        var saga = sagaHarness.Sagas.Contains(inputMessage.CorrelationId);

        // Assert
        Assert.True(await sagaHarness.Consumed.Any<TInputEvent>());

        saga.Should().NotBeNull("a saga deve existir com o CorrelationId fornecido.");
        saga.CorrelationId.Should().Be(inputMessage.CorrelationId,     "o CorrelationId da saga deve bater com o do evento publicado.");
        saga.ExternalId.Should().Be(expected.ExternalId,               "o ExternalCode deve ser copiado corretamente para a saga.");
        saga.Plate.Should().Be(expected.Plate,                         "a placa deve ser copiada corretamente para a saga.");
        saga.InfringementCode.Should().Be(expected.InfringementCode,   "o código de infração deve ser copiado corretamente para a saga.");
        saga.Amount.Should().Be(expected.Amount,                       "o valor total deve ser copiado corretamente para a saga.");
        saga.OriginSystem.Should().Be(expected.OriginSystem,           "o sistema de origem deve ser copiado corretamente para a saga.");
    }
}
