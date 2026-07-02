using Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Persistence;
using Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Repositories;

namespace Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Handlers;

/// <summary>
/// Equivalente ao <c>InvalidInfringementConsumer</c> da versão MassTransit:
/// consome o tópico de infrações inválidas (DLQ) e persiste no MongoDB para retry manual.
/// </summary>
public class InfringementValidationFailedHandler
{
    public static async Task Handle(
        InfringementValidationFailed message,
        IInvalidInfringementRepository repository,
        ILogger<InfringementValidationFailedHandler> logger,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Infração inválida recebida na DLQ. OriginId: {OriginId}, Sistema: {OriginSystem}, Motivo: {Reason}",
            message.OriginId, message.OriginSystem, message.FailureReason);

        var document = new InvalidInfringementDocument
        {
            CorrelationId = message.CorrelationId,
            OriginId = message.OriginId,
            OriginSystem = message.OriginSystem,
            Plate = message.Plate,
            InfringementCode = message.InfringementCode,
            Amount = message.Amount,
            FailureReason = message.FailureReason,
            FailedAt = message.FailedAt
        };

        await repository.SaveAsync(document, cancellationToken);
    }
}
