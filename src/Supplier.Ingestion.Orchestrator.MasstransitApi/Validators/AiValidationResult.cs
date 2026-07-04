namespace Supplier.Ingestion.Orchestrator.MasstransitApi.Validators;

public record AiValidationResult(
    bool IsValid,
    bool IsSuspicious,
    string Analysis,
    double Confidence);
