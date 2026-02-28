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

[Binding]
[Scope(Feature = "Supplier B State Machine")]
public class SupplierBStateMachineStepDefinitions : IAsyncDisposable
{
    private readonly Mock<ITopicProducer<string, UnifiedInfringementProcessed>> _unifiedProducerMock = new();
    private readonly Mock<ITopicProducer<string, InfringementValidationFailed>> _failedProducerMock = new();

    private ServiceProvider? _provider;
    private ITestHarness _harness = null!;
    private ISagaStateMachineTestHarness<SupplierBStateMachine, SupplierState> _sagaHarness = null!;
    private Guid _correlationId;
    private SupplierBInputReceived _inputEvent = null!;

    private async Task EnsureHarnessStarted()
    {
        if (_provider is not null)
            return;

        _provider = new ServiceCollection()
            .AddSingleton(Mock.Of<ILogger<SupplierBStateMachine>>())
            .AddSingleton(_unifiedProducerMock.Object)
            .AddSingleton(_failedProducerMock.Object)
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<SupplierBStateMachine, SupplierState>();
            })
            .BuildServiceProvider(true);

        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
        _sagaHarness = _harness.GetSagaStateMachineHarness<SupplierBStateMachine, SupplierState>();
    }

    [Given(@"a valid infringement event from Supplier B with plate ""(.*)"" and amount (.*)")]
    public void GivenAValidInfringementEventFromSupplierB(string plate, decimal amount)
    {
        _correlationId = Guid.NewGuid();
        _inputEvent = new SupplierBInputReceived($"EXT-{_correlationId}", plate, 5678, amount)
        {
            CorrelationId = _correlationId
        };
    }

    [Given(@"an invalid infringement event from Supplier B with plate ""(.*)"" and amount (.*)")]
    public void GivenAnInvalidInfringementEventFromSupplierB(string plate, decimal amount)
    {
        _correlationId = Guid.NewGuid();
        _inputEvent = new SupplierBInputReceived($"EXT-{_correlationId}", plate, 5678, amount)
        {
            CorrelationId = _correlationId
        };
    }

    [When(@"the event is published to the bus")]
    public async Task WhenTheEventIsPublishedToTheBus()
    {
        await EnsureHarnessStarted();
        await _harness.Bus.Publish(_inputEvent);
    }

    [Then(@"the saga should be finalized")]
    public async Task ThenTheSagaShouldBeFinalized()
    {
        var consumed = await _sagaHarness.Consumed.Any<SupplierBInputReceived>(
            x => x.Context.Message.CorrelationId == _correlationId);
        consumed.Should().BeTrue();

        var existsInFinal = await _sagaHarness.Exists(_correlationId, x => x.Final);
        existsInFinal.Should().NotBe(Guid.Empty);
    }

    [Then(@"a unified infringement processed event should be produced")]
    public void ThenAUnifiedInfringementProcessedEventShouldBeProduced()
    {
        _unifiedProducerMock.Verify(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<UnifiedInfringementProcessed>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Then(@"no validation failed event should be produced")]
    public void ThenNoValidationFailedEventShouldBeProduced()
    {
        _failedProducerMock.Verify(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<InfringementValidationFailed>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Then(@"a validation failed event should be produced")]
    public void ThenAValidationFailedEventShouldBeProduced()
    {
        _failedProducerMock.Verify(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<InfringementValidationFailed>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Then(@"no unified infringement processed event should be produced")]
    public void ThenNoUnifiedInfringementProcessedEventShouldBeProduced()
    {
        _unifiedProducerMock.Verify(p => p.Produce(
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
