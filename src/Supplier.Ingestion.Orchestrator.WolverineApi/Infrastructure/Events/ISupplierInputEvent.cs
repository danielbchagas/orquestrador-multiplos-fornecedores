namespace Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Events;

public interface ISupplierInputEvent
{
    Guid CorrelationId { get; }
    string ExternalCode { get; }
    string Plate { get; }
    int Infringement { get; }
    decimal TotalValue { get; }
    string OriginSystem { get; }
}
