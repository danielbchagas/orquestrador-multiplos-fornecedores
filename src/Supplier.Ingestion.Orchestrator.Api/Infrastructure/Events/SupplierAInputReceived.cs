namespace Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;

public record SupplierAInputReceived : IntegrationEvent
{
    public SupplierAInputReceived(
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
    public string OriginSystem { get; init; } = "SupplierA";
}