using AutoFixture;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;

namespace Supplier.Ingestion.Orchestrator.Tests.UnitTests.Infrastructure.StateMachines;

public class SupplierBStateMachineTests
{
    private readonly IFixture _fixture = new Fixture();
    private readonly Mock<ITopicProducer<string, UnifiedInfringementProcessed>> _unifiedProducerMock;
    private readonly Mock<ITopicProducer<string, InfringementValidationFailed>> _failedProducerMock;

    public SupplierBStateMachineTests()
    {
        _unifiedProducerMock = new Mock<ITopicProducer<string, UnifiedInfringementProcessed>>();
        _failedProducerMock = new Mock<ITopicProducer<string, InfringementValidationFailed>>();
    }

    [Fact]
    public async Task GivenValidInput_WhenInputReceived_ShouldProduceUnifiedMessageAndFinalize()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SupplierBStateMachine>>();
        
        await using var provider = new ServiceCollection()
            .AddSingleton(loggerMock.Object)
            .AddSingleton(_unifiedProducerMock.Object)
            .AddSingleton(_failedProducerMock.Object)
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<SupplierBStateMachine, InfringementState>();
            })
            .BuildServiceProvider(true);

        var sagaHarness = provider.GetRequiredService<ITestHarness>();
        await sagaHarness.Start();

        var inputEvent = new SupplierBInputReceived(
            _fixture.Create<string>(),
            "ABC1234",
            _fixture.Create<int>(),
            _fixture.Create<decimal>() + 1 // Ensure positive
        );

        // Act
        await sagaHarness.Bus.Publish(inputEvent);

        // Assert
        Assert.True(await sagaHarness.Consumed.Any<SupplierBInputReceived>());

        _unifiedProducerMock.Verify(
            p => p.Produce(
                inputEvent.ExternalCode,
                It.Is<UnifiedInfringementProcessed>(m =>
                    m.OriginId == inputEvent.ExternalCode &&
                    m.Plate == inputEvent.Plate &&
                    m.InfringementCode == inputEvent.Infringement &&
                    m.Amount == inputEvent.TotalValue &&
                    m.SourceSystem == inputEvent.OriginSystem
                ),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _failedProducerMock.Verify(
            p => p.Produce(It.IsAny<string>(), It.IsAny<InfringementValidationFailed>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GivenInvalidInput_WhenInputReceived_ShouldProduceValidationFailedMessageAndFinalize()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SupplierBStateMachine>>();
        
        await using var provider = new ServiceCollection()
            .AddSingleton(loggerMock.Object)
            .AddSingleton(_unifiedProducerMock.Object)
            .AddSingleton(_failedProducerMock.Object)
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<SupplierBStateMachine, InfringementState>();
            })
            .BuildServiceProvider(true);

        var sagaHarness = provider.GetRequiredService<ITestHarness>();
        await sagaHarness.Start();

        var inputEvent = _fixture.Build<SupplierBInputReceived>()
            .With(x => x.TotalValue, -1)
            .Create();

        _unifiedProducerMock
            .Setup(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<UnifiedInfringementProcessed>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _failedProducerMock
            .Setup(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<InfringementValidationFailed>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await sagaHarness.Bus.Publish(inputEvent);

        // Assert
        Assert.True(await sagaHarness.Consumed.Any<SupplierBInputReceived>());

        _unifiedProducerMock.Verify(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<UnifiedInfringementProcessed>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _failedProducerMock.Verify(p => p.Produce(
                inputEvent.ExternalCode,
                It.IsAny<InfringementValidationFailed>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}