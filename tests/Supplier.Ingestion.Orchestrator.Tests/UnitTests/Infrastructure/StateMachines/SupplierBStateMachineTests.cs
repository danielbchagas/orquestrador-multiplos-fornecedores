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
        await using var provider = new ServiceCollection()
            .AddSingleton(Mock.Of<ILogger<SupplierBStateMachine>>())
            .AddSingleton(_unifiedProducerMock.Object)
            .AddSingleton(_failedProducerMock.Object)
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<SupplierBStateMachine, SupplierState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var machine = provider.GetRequiredService<SupplierBStateMachine>();
        var sagaHarness = harness.GetSagaStateMachineHarness<SupplierBStateMachine, SupplierState>();

        var correlationId = Guid.NewGuid();
        var inputEvent = _fixture.Build<SupplierBInputReceived>()
            .With(x => x.CorrelationId, correlationId)
            .With(x => x.TotalValue, 150.75m)
            .Create();

        // Act
        await harness.Bus.Publish(inputEvent);

        // Assert
        Assert.True(await sagaHarness.Consumed.Any<SupplierBInputReceived>(x => x.Context.Message.CorrelationId == correlationId));
        Assert.NotEqual(await sagaHarness.Exists(correlationId, x => x.Final), Guid.Empty);

        _unifiedProducerMock.Verify(
            p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<UnifiedInfringementProcessed>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _failedProducerMock.Verify(
            p => p.Produce(
                It.IsAny<string>(), 
                It.IsAny<InfringementValidationFailed>(), 
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GivenInvalidInput_WhenInputReceived_ShouldProduceValidationFailedMessageAndFinalize()
    {
        // Arrange
        await using var provider = new ServiceCollection()
            .AddSingleton(Mock.Of<ILogger<SupplierBStateMachine>>())
            .AddSingleton(_unifiedProducerMock.Object)
            .AddSingleton(_failedProducerMock.Object)
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<SupplierBStateMachine, SupplierState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var machine = provider.GetRequiredService<SupplierBStateMachine>();
        var sagaHarness = harness.GetSagaStateMachineHarness<SupplierBStateMachine, SupplierState>();

        var correlationId = Guid.NewGuid();
        var inputEvent = _fixture.Build<SupplierBInputReceived>()
            .With(x => x.CorrelationId, correlationId)
            .With(x => x.TotalValue, -1)
            .Create();

        // Act
        await harness.Bus.Publish(inputEvent);

        // Assert
        Assert.True(await sagaHarness.Consumed.Any<SupplierBInputReceived>(x => x.Context.Message.CorrelationId == correlationId));
        Assert.NotEqual(await sagaHarness.Exists(correlationId, x => x.Final), Guid.Empty);

        _unifiedProducerMock.Verify(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<UnifiedInfringementProcessed>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _failedProducerMock.Verify(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<InfringementValidationFailed>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}