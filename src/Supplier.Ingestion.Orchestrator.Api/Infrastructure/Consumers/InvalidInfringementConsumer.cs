using MassTransit;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Persistence;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Repositories;

namespace Supplier.Ingestion.Orchestrator.Api.Infrastructure.Consumers;

public class InvalidInfringementConsumer(
    IInvalidInfringementRepository repository,
    ILogger<InvalidInfringementConsumer> logger) : IConsumer<InfringementValidationFailed>
{
    public async Task Consume(ConsumeContext<InfringementValidationFailed> context)
    {
        var msg = context.Message;

        logger.LogWarning(
            "Infração inválida recebida na DLQ. OriginId: {OriginId}, Sistema: {OriginSystem}, Motivo: {Reason}",
            msg.OriginId, msg.OriginSystem, msg.FailureReason);

        var document = new InvalidInfringementDocument
        {
            CorrelationId = msg.CorrelationId,
            OriginId = msg.OriginId,
            OriginSystem = msg.OriginSystem,
            Plate = msg.Plate,
            InfringementCode = msg.InfringementCode,
            Amount = msg.Amount,
            FailureReason = msg.FailureReason,
            FailedAt = msg.FailedAt
        };

        await repository.SaveAsync(document, context.CancellationToken);
    }
}
