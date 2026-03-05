namespace Supplier.Ingestion.Orchestrator.Api.Validators;

public record ValidationResult(
    bool IsValid,
    string Errors,
    float ConfidenceScore,
    string[] RiskFlags);
