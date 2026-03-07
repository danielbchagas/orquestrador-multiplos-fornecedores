namespace Supplier.Ingestion.Orchestrator.Api.Validators;

public record AiValidationResult(
    bool IsValid,
    bool IsSuspicious,
    string Analysis,
    double Confidence);
