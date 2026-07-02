namespace Supplier.Ingestion.Orchestrator.WolverineApi.Validators;

public record AiValidationResult(
    bool IsValid,
    bool IsSuspicious,
    string Analysis,
    double Confidence);
