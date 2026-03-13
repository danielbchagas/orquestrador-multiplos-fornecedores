using AutoFixture;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;
using Supplier.Ingestion.Orchestrator.Api.Validators;

namespace Supplier.Ingestion.Orchestrator.Tests.UnitTests.Infrastructure.StateMachines;

public abstract class SupplierStateMachineTestsBase<TStateMachine, TInputEvent>
    where TStateMachine : class, MassTransit.SagaStateMachine<SupplierState>
    where TInputEvent : class, MassTransit.CorrelatedBy<Guid>
{
    protected readonly IFixture Fixture = new Fixture();
    protected readonly Mock<ITopicProducer<string, UnifiedInfringementProcessed>> UnifiedProducerMock;
    protected readonly Mock<ITopicProducer<string, InfringementValidationFailed>> FailedProducerMock;
    protected readonly Mock<IAiInfringementValidator> AiValidatorMock;
    protected readonly Mock<IInfringementValidator> InfringementValidatorMock;

    protected SupplierStateMachineTestsBase()
    {
        UnifiedProducerMock = new Mock<ITopicProducer<string, UnifiedInfringementProcessed>>();
        FailedProducerMock = new Mock<ITopicProducer<string, InfringementValidationFailed>>();
        AiValidatorMock = new Mock<IAiInfringementValidator>();
        InfringementValidatorMock = new Mock<IInfringementValidator>();

        InfringementValidatorMock
            .Setup(v => v.Validate(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>()))
            .Returns<string, decimal, string>((plate, amount, externalId) =>
                new InfringementValidator().Validate(plate, amount, externalId));

        AiValidatorMock
            .Setup(v => v.ValidateAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiValidationResult(true, false, "AI validation OK", 0.95));
    }

    protected abstract TInputEvent BuildValidInputEvent(Guid correlationId);
    protected abstract TInputEvent BuildInvalidInputEvent(Guid correlationId);

    private async Task<(ITestHarness harness, ISagaStateMachineTestHarness<TStateMachine, SupplierState> sagaHarness)> BuildHarness()
    {
        var provider = new ServiceCollection()
            .AddSingleton(Mock.Of<ILogger<TStateMachine>>())
            .AddSingleton(UnifiedProducerMock.Object)
            .AddSingleton(FailedProducerMock.Object)
            .AddSingleton(AiValidatorMock.Object)
            .AddSingleton(InfringementValidatorMock.Object)
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<TStateMachine, SupplierState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<TStateMachine, SupplierState>();

        return (harness, sagaHarness);
    }

    [Fact]
    public async Task GivenValidInput_WhenInputReceivedEventIsConsumed_ThenShouldPublishUnifiedInfringementProcessedAndFinalize()
    {
        // Arrange
        var (harness, sagaHarness) = await BuildHarness();
        var correlationId = Guid.NewGuid();
        var inputEvent = BuildValidInputEvent(correlationId);

        // Act
        await harness.Bus.Publish(inputEvent);

        // Assert
        Assert.True(await sagaHarness.Consumed.Any<TInputEvent>(x => x.Context.Message.CorrelationId == correlationId));
        Assert.NotEqual(await sagaHarness.Exists(correlationId, x => x.Final), Guid.Empty);

        UnifiedProducerMock.Verify(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<UnifiedInfringementProcessed>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        FailedProducerMock.Verify(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<InfringementValidationFailed>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GivenInvalidInput_WhenInputReceivedEventIsConsumed_ThenShouldPublishInfringementValidationFailedAndFinalize()
    {
        // Arrange
        var (harness, sagaHarness) = await BuildHarness();
        var correlationId = Guid.NewGuid();
        var inputEvent = BuildInvalidInputEvent(correlationId);

        // Act
        await harness.Bus.Publish(inputEvent);

        // Assert
        Assert.True(await sagaHarness.Consumed.Any<TInputEvent>(x => x.Context.Message.CorrelationId == correlationId));
        Assert.NotEqual(await sagaHarness.Exists(correlationId, x => x.Final), Guid.Empty);

        UnifiedProducerMock.Verify(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<UnifiedInfringementProcessed>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        FailedProducerMock.Verify(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<InfringementValidationFailed>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GivenValidInput_WhenAiFlagsSuspicious_ThenShouldPublishInfringementValidationFailedAndFinalize()
    {
        // Arrange
        AiValidatorMock
            .Setup(v => v.ValidateAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiValidationResult(true, true, "Placa com formato suspeito", 0.85));

        var (harness, sagaHarness) = await BuildHarness();
        var correlationId = Guid.NewGuid();
        var inputEvent = BuildValidInputEvent(correlationId);

        // Act
        await harness.Bus.Publish(inputEvent);

        // Assert
        Assert.True(await sagaHarness.Consumed.Any<TInputEvent>(x => x.Context.Message.CorrelationId == correlationId));
        Assert.NotEqual(await sagaHarness.Exists(correlationId, x => x.Final), Guid.Empty);

        UnifiedProducerMock.Verify(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<UnifiedInfringementProcessed>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        FailedProducerMock.Verify(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<InfringementValidationFailed>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GivenValidInput_WhenAiValidationUnavailable_ThenShouldPublishUnifiedInfringementProcessedAndFinalize()
    {
        // Arrange
        AiValidatorMock
            .Setup(v => v.ValidateAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiValidationResult(true, false, "AI validation unavailable", 0.5));

        var (harness, sagaHarness) = await BuildHarness();
        var correlationId = Guid.NewGuid();
        var inputEvent = BuildValidInputEvent(correlationId);

        // Act
        await harness.Bus.Publish(inputEvent);

        // Assert
        Assert.True(await sagaHarness.Consumed.Any<TInputEvent>(x => x.Context.Message.CorrelationId == correlationId));
        Assert.NotEqual(await sagaHarness.Exists(correlationId, x => x.Final), Guid.Empty);

        UnifiedProducerMock.Verify(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<UnifiedInfringementProcessed>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
