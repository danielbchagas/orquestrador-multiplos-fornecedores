namespace Supplier.Ingestion.Orchestrator.Api.Domain.Events;

public record SupplierBInputReceived : IntegrationEvent
{
    public SupplierBInputReceived(
        string externalCode,
        string plate,
        int infringement,
        decimal totalValue)
        : base(externalCode)
    {
        ExternalCode = externalCode;
        Plate = plate;
        Infringement = infringement;
        TotalValue = totalValue;
    }

    public string ExternalCode { get; init; }
    public string Plate { get; init; }
    public int Infringement { get; init; }
    public decimal TotalValue { get; init; }
    public string OriginSystem { get; init; } = "SupplierB";
}