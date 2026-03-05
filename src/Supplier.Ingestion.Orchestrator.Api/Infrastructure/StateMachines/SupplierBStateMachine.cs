using MassTransit;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.Api.Validators;

namespace Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;

public class SupplierBStateMachine : MassTransitStateMachine<SupplierState>
{
    public Event<SupplierBInputReceived> InputReceived { get; private set; }

    public SupplierBStateMachine(ILogger<SupplierBStateMachine> logger)
    {
        InstanceState(x => x.CurrentState);

        Event(() => InputReceived, x =>
        {
            x.SelectId(ctx => ctx.Message.CorrelationId);
            x.InsertOnInitial = true;
        });

        Initially(
            When(InputReceived)
                .Then(ctx =>
                {
                    logger.LogInformation("Saga B started. ExternalCode: {ExternalCode}", ctx.Message.ExternalCode);

                    ctx.Saga.CorrelationId = ctx.Message.CorrelationId;
                    ctx.Saga.ExternalId = ctx.Message.ExternalCode;
                    ctx.Saga.Plate = ctx.Message.Plate;
                    ctx.Saga.Amount = ctx.Message.TotalValue;
                    ctx.Saga.OriginSystem = ctx.Message.OriginSystem;
                    ctx.Saga.InfringementCode = ctx.Message.Infringement;
                    ctx.Saga.CreatedAt = DateTime.UtcNow;

                    var result = InfringementValidator.Validate(
                        ctx.Saga.Plate,
                        ctx.Saga.Amount,
                        ctx.Saga.ExternalId
                    );

                    ctx.Saga.IsValid = result.IsValid;
                    ctx.Saga.ValidationErrors = result.Errors;
                    ctx.Saga.ConfidenceScore = result.ConfidenceScore;
                    ctx.Saga.RiskFlags = result.RiskFlags;

                    logger.LogInformation(
                        "Validation completed. IsValid: {IsValid}, ConfidenceScore: {ConfidenceScore}, RiskFlags: {RiskFlags}",
                        ctx.Saga.IsValid, ctx.Saga.ConfidenceScore, string.Join(", ", ctx.Saga.RiskFlags));
                })
                .IfElse(
                    ctx => ctx.Saga.IsValid,

                    binder => binder.ThenAsync(async ctx =>
                    {
                        var producer = ctx.GetPayload<IServiceProvider>()
                            .GetRequiredService<ITopicProducer<string, UnifiedInfringementProcessed>>();

                        await producer.Produce(
                            ctx.Saga.ExternalId,
                            new UnifiedInfringementProcessed(
                                ctx.Saga.ExternalId,
                                ctx.Saga.Plate,
                                ctx.Saga.InfringementCode,
                                ctx.Saga.Amount,
                                ctx.Saga.OriginSystem,
                                ctx.Saga.ConfidenceScore,
                                ctx.Saga.RiskFlags
                            ),
                            ctx.CancellationToken
                        );

                        logger.LogInformation("Message sent to Kafka (Success)!");
                    })
                    .Finalize(),

                    binder => binder.ThenAsync(async ctx =>
                    {
                        var producer = ctx.GetPayload<IServiceProvider>()
                            .GetRequiredService<ITopicProducer<string, InfringementValidationFailed>>();

                        await producer.Produce(
                            ctx.Saga.ExternalId,
                            new InfringementValidationFailed(
                                ctx.Saga.ExternalId,
                                ctx.Saga.OriginSystem,
                                ctx.Saga.ValidationErrors,
                                ctx.Saga.ConfidenceScore
                            ),
                            ctx.CancellationToken
                        );

                        logger.LogWarning("Message sent to Kafka (DLQ)!");
                    })
                    .Finalize()
                )
        );

        SetCompletedWhenFinalized();
    }
}