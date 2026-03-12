namespace Supplier.Ingestion.Orchestrator.Api.Validators;

public interface IInfringementValidator
{
    (bool IsValid, string Errors) Validate(string plate, decimal amount, string externalId);
}
