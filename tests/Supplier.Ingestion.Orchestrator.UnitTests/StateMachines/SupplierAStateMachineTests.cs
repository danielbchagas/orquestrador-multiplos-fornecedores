using AutoFixture;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Supplier.Ingestion.Orchestrator.Api.Domain.Events;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;

namespace Supplier.Ingestion.Orchestrator.UnitTests.StateMachines;

public class SupplierAStateMachineTests
{
    private readonly IFixture _fixture = new Fixture();
    private readonly Mock<ITopicProducer<string, UnifiedInfringementProcessed>> _unifiedProducerMock;
    private readonly Mock<ITopicProducer<string, InfringementValidationFailed>> _failedProducerMock;

    public SupplierAStateMachineTests()
    {
        _unifiedProducerMock = new Mock<ITopicProducer<string, UnifiedInfringementProcessed>>();
        _failedProducerMock = new Mock<ITopicProducer<string, InfringementValidationFailed>>();
    }

    [Fact]
    public async Task GivenValidInput_WhenInputReceivedEventIsConsumed_ThenShouldPublishUnifiedInfringementProcessedAndFinalize()
    {
        // Arrange
        await using var provider = new ServiceCollection()
            .AddSingleton(Mock.Of<ILogger<SupplierAStateMachine>>())
            .AddSingleton(_unifiedProducerMock.Object)
            .AddSingleton(_failedProducerMock.Object)
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<SupplierAStateMachine, InfringementState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var machine = provider.GetRequiredService<SupplierAStateMachine>();
        var sagaHarness = harness.GetSagaStateMachineHarness<SupplierAStateMachine, InfringementState>();

        var sagaId = Guid.NewGuid();
        var inputEvent = _fixture.Build<SupplierAInputReceived>()
            .With(x => x.TotalValue, 100) // Ensure amount is valid
            .Create();

        // Act
        await harness.Bus.Publish(inputEvent);

        // Assert
        _unifiedProducerMock.Verify(p => p.Produce(
                inputEvent.ExternalId,
                It.Is<UnifiedInfringementProcessed>(msg =>
                    msg.OriginId == inputEvent.ExternalId &&
                    msg.Plate == inputEvent.Plate &&
                    msg.Amount == inputEvent.TotalValue),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _failedProducerMock.Verify(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<InfringementValidationFailed>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("", 100, "Placa invalida")]
    [InlineData("ABC-1234", -10, "Valor invalido")]
    [InlineData("", -10, "Placa invalida,Valor invalido")]
    public async Task GivenInvalidInput_WhenInputReceivedEventIsConsumed_ThenShouldPublishInfringementValidationFailedAndFinalize(
        string plate, decimal amount, string expectedError)
    {
        // Arrange
        await using var provider = new ServiceCollection()
            .AddSingleton(Mock.Of<ILogger<SupplierAStateMachine>>())
            .AddSingleton(_unifiedProducerMock.Object)
            .AddSingleton(_failedProducerMock.Object)
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<SupplierAStateMachine, InfringementState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var machine = provider.GetRequiredService<SupplierAStateMachine>();
        var sagaHarness = harness.GetSagaStateMachineHarness<SupplierAStateMachine, InfringementState>();

        var inputEvent = _fixture.Build<SupplierAInputReceived>()
            .With(x => x.Plate, plate)
            .With(x => x.TotalValue, amount)
            .Create();

        // Act
        await harness.Bus.Publish(inputEvent);

        // Assert
        Assert.True(await sagaHarness.Consumed.Any<SupplierAInputReceived>());

        _unifiedProducerMock.Verify(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<UnifiedInfringementProcessed>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _failedProducerMock.Verify(p => p.Produce(
                inputEvent.ExternalId,
                It.Is<InfringementValidationFailed>(msg =>
                    msg.OriginId == inputEvent.ExternalId &&
                    msg.FailureReason == expectedError),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}