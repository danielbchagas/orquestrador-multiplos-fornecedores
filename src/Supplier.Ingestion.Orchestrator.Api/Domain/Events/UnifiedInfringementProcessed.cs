namespace Supplier.Ingestion.Orchestrator.Api.Domain.Events;

public record UnifiedInfringementProcessed : IntegrationEvent
{
    public UnifiedInfringementProcessed(
        string originId,
        string plate,
        int infringementCode,
        decimal amount,
        string sourceSystem)
        : base(originId)
    {
        OriginId = originId;
        Plate = plate;
        InfringementCode = infringementCode;
        Amount = amount;
        SourceSystem = sourceSystem;
        ProcessedAt = DateTime.UtcNow;
    }

    public string OriginId { get; init; }
    public string Plate { get; init; }
    public int InfringementCode { get; init; }
    public decimal Amount { get; init; }
    public string SourceSystem { get; init; }
    public DateTime ProcessedAt { get; init; }
}