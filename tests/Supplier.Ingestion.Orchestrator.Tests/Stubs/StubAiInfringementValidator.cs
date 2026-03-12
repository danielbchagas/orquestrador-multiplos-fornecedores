using Supplier.Ingestion.Orchestrator.Api.Validators;

namespace Supplier.Ingestion.Orchestrator.Tests;

public class StubAiInfringementValidator : IAiInfringementValidator
{
    public Task<AiValidationResult> ValidateAsync(
        string plate,
        int infringementCode,
        decimal amount,
        string originSystem,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new AiValidationResult(true, false, "Stub AI validation OK", 0.95));
    }
}
