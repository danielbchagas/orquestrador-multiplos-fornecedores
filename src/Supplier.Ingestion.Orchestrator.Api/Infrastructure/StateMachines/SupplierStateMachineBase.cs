using MassTransit;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.Api.Security;
using Supplier.Ingestion.Orchestrator.Api.Validators;

namespace Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;

public abstract class SupplierStateMachineBase<TInputEvent> : MassTransitStateMachine<SupplierState>
    where TInputEvent : class, ISupplierInputEvent
{
    public Event<TInputEvent> InputReceived { get; private set; }

    protected SupplierStateMachineBase(ILogger logger, string supplierName)
    {
        InstanceState(x => x.CurrentState);

        Event(() => InputReceived, x =>
        {
            x.SelectId(ctx => ctx.Message.CorrelationId);
            x.InsertOnInitial = true;
        });

        Initially(
            When(InputReceived)
                .ThenAsync(async ctx =>
                {
                    logger.LogInformation("Saga {Supplier} iniciada. ExternalCode: {ExternalCode}",
                        supplierName, ctx.Message.ExternalCode);

                    ctx.Saga.CorrelationId = ctx.Message.CorrelationId;
                    ctx.Saga.ExternalId = ctx.Message.ExternalCode;
                    ctx.Saga.Plate = ctx.Message.Plate;
                    ctx.Saga.Amount = ctx.Message.TotalValue;
                    ctx.Saga.OriginSystem = ctx.Message.OriginSystem;
                    ctx.Saga.InfringementCode = ctx.Message.Infringement;
                    ctx.Saga.CreatedAt = DateTime.UtcNow;

                    var infringementValidator = ctx.GetPayload<IServiceProvider>()
                        .GetRequiredService<IInfringementValidator>();

                    var (isValid, error) = infringementValidator.Validate(
                        ctx.Saga.Plate,
                        ctx.Saga.Amount,
                        ctx.Saga.ExternalId
                    );

                    ctx.Saga.IsValid = isValid;
                    ctx.Saga.ValidationErrors = error;

                    if (isValid)
                    {
                        var aiValidator = ctx.GetPayload<IServiceProvider>()
                            .GetRequiredService<IAiInfringementValidator>();

                        var aiResult = await aiValidator.ValidateAsync(
                            ctx.Saga.Plate,
                            ctx.Saga.InfringementCode,
                            ctx.Saga.Amount,
                            ctx.Saga.OriginSystem,
                            ctx.CancellationToken
                        );

                        ctx.Saga.AiAnalysis = aiResult.Analysis;
                        ctx.Saga.AiIsSuspicious = aiResult.IsSuspicious;

                        if (!aiResult.IsValid || aiResult.IsSuspicious)
                        {
                            ctx.Saga.IsValid = false;
                            ctx.Saga.ValidationErrors = $"AI: {aiResult.Analysis}";
                        }

                        logger.LogInformation(
                            "Validação IA concluída. Placa: {Plate}, Suspeito: {IsSuspicious}, Confiança: {Confidence}, Análise: {Analysis}",
                            PlateObfuscator.Mask(ctx.Saga.Plate), aiResult.IsSuspicious, aiResult.Confidence, aiResult.Analysis);
                    }

                    ctx.Saga.UpdatedAt = DateTime.UtcNow;

                    logger.LogInformation("Validação concluída. Válido: {IsValid}", ctx.Saga.IsValid);
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
                                ctx.Saga.OriginSystem
                            ),
                            ctx.CancellationToken
                        );

                        ctx.Saga.ProcessedAt = DateTime.UtcNow;
                        logger.LogInformation("Mensagem enviada ao Kafka (sucesso).");
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
                                ctx.Saga.Plate,
                                ctx.Saga.InfringementCode,
                                ctx.Saga.Amount,
                                ctx.Saga.ValidationErrors
                            ),
                            ctx.CancellationToken
                        );

                        ctx.Saga.ProcessedAt = DateTime.UtcNow;
                        logger.LogWarning("Mensagem enviada ao Kafka (DLQ).");
                    })
                    .Finalize()
                )
        );

        During(Final,
            Ignore(InputReceived));
    }
}
