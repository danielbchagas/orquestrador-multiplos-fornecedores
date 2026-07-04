namespace Supplier.Ingestion.Orchestrator.MasstransitApi.Validators;

public interface IInfringementValidator
{
    (bool IsValid, string Errors) Validate(string plate, decimal amount, string externalId);
}
