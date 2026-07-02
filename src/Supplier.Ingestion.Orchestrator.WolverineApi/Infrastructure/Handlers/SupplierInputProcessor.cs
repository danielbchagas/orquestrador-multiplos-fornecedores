using Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Persistence;
using Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Repositories;
using Supplier.Ingestion.Orchestrator.WolverineApi.Security;
using Supplier.Ingestion.Orchestrator.WolverineApi.Validators;
using Wolverine;

namespace Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Handlers;

/// <summary>
/// Pipeline de processamento das infrações — equivalente ao <c>SupplierStateMachineBase</c>
/// da versão MassTransit, porém como um handler direto (idiomático em Wolverine): o fluxo
/// inicia e finaliza com uma única mensagem, então não há necessidade de uma saga real.
/// A idempotência é garantida pela inserção do estado com CorrelationId determinístico como _id.
/// </summary>
public class SupplierInputProcessor(
    IInfringementValidator infringementValidator,
    IAiInfringementValidator aiValidator,
    ISupplierIngestionStateRepository stateRepository,
    ILogger<SupplierInputProcessor> logger)
{
    public async Task ProcessAsync(ISupplierInputEvent message, IMessageBus bus, CancellationToken ct)
    {
        logger.LogInformation("Processamento {Supplier} iniciado. ExternalCode: {ExternalCode}",
            message.OriginSystem, message.ExternalCode);

        var state = new SupplierIngestionState
        {
            CorrelationId = message.CorrelationId,
            ExternalId = message.ExternalCode,
            Plate = message.Plate,
            Amount = message.TotalValue,
            OriginSystem = message.OriginSystem,
            InfringementCode = message.Infringement,
            CreatedAt = DateTime.UtcNow
        };

        // Barreira de idempotência: equivalente ao InsertOnInitial + During(Final, Ignore(...)) da saga
        if (!await stateRepository.TryBeginProcessingAsync(state, ct))
        {
            logger.LogInformation(
                "Mensagem duplicada ignorada. CorrelationId: {CorrelationId}, ExternalCode: {ExternalCode}",
                message.CorrelationId, message.ExternalCode);
            return;
        }

        var (isValid, error) = infringementValidator.Validate(state.Plate, state.Amount, state.ExternalId);

        state.IsValid = isValid;
        state.ValidationErrors = error;

        if (isValid)
        {
            var aiResult = await aiValidator.ValidateAsync(
                state.Plate,
                state.InfringementCode,
                state.Amount,
                state.OriginSystem,
                ct);

            state.AiAnalysis = aiResult.Analysis;
            state.AiIsSuspicious = aiResult.IsSuspicious;

            if (!aiResult.IsValid || aiResult.IsSuspicious)
            {
                state.IsValid = false;
                state.ValidationErrors = $"AI: {aiResult.Analysis}";
            }

            logger.LogInformation(
                "Validação IA concluída. Placa: {Plate}, Suspeito: {IsSuspicious}, Confiança: {Confidence}, Análise: {Analysis}",
                PlateObfuscator.Mask(state.Plate), aiResult.IsSuspicious, aiResult.Confidence, aiResult.Analysis);
        }

        state.UpdatedAt = DateTime.UtcNow;

        logger.LogInformation("Validação concluída. Válido: {IsValid}", state.IsValid);

        // A chave da mensagem Kafka (PartitionKey) segue o mesmo padrão da versão MassTransit: o id de origem
        var delivery = new DeliveryOptions { PartitionKey = state.ExternalId };

        if (state.IsValid)
        {
            await bus.PublishAsync(
                new UnifiedInfringementProcessed(
                    state.ExternalId,
                    state.Plate,
                    state.InfringementCode,
                    state.Amount,
                    state.OriginSystem),
                delivery);

            state.CurrentState = "Processed";
            logger.LogInformation("Mensagem enviada ao Kafka (sucesso).");
        }
        else
        {
            await bus.PublishAsync(
                new InfringementValidationFailed(
                    state.ExternalId,
                    state.OriginSystem,
                    state.Plate,
                    state.InfringementCode,
                    state.Amount,
                    state.ValidationErrors),
                delivery);

            state.CurrentState = "Rejected";
            logger.LogWarning("Mensagem enviada ao Kafka (DLQ).");
        }

        state.ProcessedAt = DateTime.UtcNow;
        await stateRepository.SaveAsync(state, ct);
    }
}
