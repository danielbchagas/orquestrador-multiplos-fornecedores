namespace Supplier.Ingestion.Orchestrator.Api.Domain.Events;

public record SupplierAInputReceived : IntegrationEvent
{
    public SupplierAInputReceived(
        string externalId,
        string plate,
        int infringement,
        decimal totalValue)
        : base(externalId)
    {
        ExternalId = externalId;
        Plate = plate;
        Infringement = infringement;
        TotalValue = totalValue;
    }

    public string ExternalId { get; init; }
    public string Plate { get; init; }
    public int Infringement { get; init; }
    public decimal TotalValue { get; init; }
    public string OriginSystem { get; init; } = "SupplierA";
}