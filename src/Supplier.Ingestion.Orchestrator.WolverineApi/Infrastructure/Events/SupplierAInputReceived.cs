namespace Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Events;

public record SupplierAInputReceived(
    Guid CorrelationId,
    string ExternalCode,
    string Plate,
    int Infringement,
    decimal TotalValue,
    string OriginSystem = "SupplierA") : ISupplierInputEvent
{
    public static SupplierAInputReceived Create(string externalCode, string plate, int infringement, decimal totalValue)
        => new(DeterministicId.FromBusinessKey(externalCode), externalCode, plate, infringement, totalValue);
}
