using MassTransit;

namespace Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;

public interface ISupplierInputEvent : CorrelatedBy<Guid>
{
    string ExternalCode { get; }
    string Plate { get; }
    int Infringement { get; }
    decimal TotalValue { get; }
    string OriginSystem { get; }
}
