namespace Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Events;

public record SupplierBInputReceived(
    Guid CorrelationId,
    string ExternalCode,
    string Plate,
    int Infringement,
    decimal TotalValue,
    string OriginSystem = "SupplierB") : ISupplierInputEvent
{
    public static SupplierBInputReceived Create(string externalCode, string plate, int infringement, decimal totalValue)
        => new(DeterministicId.FromBusinessKey(externalCode), externalCode, plate, infringement, totalValue);
}
