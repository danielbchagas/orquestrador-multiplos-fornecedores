using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Reqnroll;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;

namespace Supplier.Ingestion.Orchestrator.Tests.FunctionalTests.StepDefinitions;

public abstract class SupplierStateMachineStepDefinitionsBase<TStateMachine, TState, TInputEvent> : IAsyncDisposable
    where TStateMachine : class, SagaStateMachine<TState>
    where TState : SupplierState, new()
    where TInputEvent : class, CorrelatedBy<Guid>
{
    protected readonly Mock<ITopicProducer<string, UnifiedInfringementProcessed>> UnifiedProducerMock = new();
    protected readonly Mock<ITopicProducer<string, InfringementValidationFailed>> FailedProducerMock = new();

    private ServiceProvider? _provider;
    private ITestHarness _harness = null!;
    private ISagaStateMachineTestHarness<TStateMachine, TState> _sagaHarness = null!;
    protected Guid CorrelationId;
    protected TInputEvent InputEvent = null!;

    private async Task EnsureHarnessStarted()
    {
        if (_provider is not null)
            return;

        _provider = new ServiceCollection()
            .AddSingleton(Mock.Of<ILogger<TStateMachine>>())
            .AddSingleton(UnifiedProducerMock.Object)
            .AddSingleton(FailedProducerMock.Object)
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<TStateMachine, TState>();
            })
            .BuildServiceProvider(true);

        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
        _sagaHarness = _harness.GetSagaStateMachineHarness<TStateMachine, TState>();
    }

    [When(@"the event is published to the bus")]
    public async Task WhenTheEventIsPublishedToTheBus()
    {
        await EnsureHarnessStarted();
        await _harness.Bus.Publish(InputEvent);
    }

    [Then(@"the saga should be finalized")]
    public async Task ThenTheSagaShouldBeFinalized()
    {
        var consumed = await _sagaHarness.Consumed.Any<TInputEvent>(
            x => x.Context.Message.CorrelationId == CorrelationId);
        consumed.Should().BeTrue();

        var existsInFinal = await _sagaHarness.Exists(CorrelationId, x => x.Final);
        existsInFinal.Should().NotBe(Guid.Empty);
    }

    [Then(@"a unified infringement processed event should be produced")]
    public void ThenAUnifiedInfringementProcessedEventShouldBeProduced()
    {
        UnifiedProducerMock.Verify(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<UnifiedInfringementProcessed>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Then(@"no validation failed event should be produced")]
    public void ThenNoValidationFailedEventShouldBeProduced()
    {
        FailedProducerMock.Verify(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<InfringementValidationFailed>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Then(@"a validation failed event should be produced")]
    public void ThenAValidationFailedEventShouldBeProduced()
    {
        FailedProducerMock.Verify(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<InfringementValidationFailed>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Then(@"no unified infringement processed event should be produced")]
    public void ThenNoUnifiedInfringementProcessedEventShouldBeProduced()
    {
        UnifiedProducerMock.Verify(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<UnifiedInfringementProcessed>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
            await _provider.DisposeAsync();
    }
}
