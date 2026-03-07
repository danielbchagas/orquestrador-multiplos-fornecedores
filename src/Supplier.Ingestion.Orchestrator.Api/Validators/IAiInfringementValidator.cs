namespace Supplier.Ingestion.Orchestrator.Api.Validators;

public interface IAiInfringementValidator
{
    Task<AiValidationResult> ValidateAsync(
        string plate,
        int infringementCode,
        decimal amount,
        string originSystem,
        CancellationToken cancellationToken = default);
}
