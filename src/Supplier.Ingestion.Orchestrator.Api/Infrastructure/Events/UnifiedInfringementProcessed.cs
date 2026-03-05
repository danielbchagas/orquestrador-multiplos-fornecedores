namespace Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;

public record UnifiedInfringementProcessed : IntegrationEvent
{
    public UnifiedInfringementProcessed(
        string originId,
        string plate,
        int infringementCode,
        decimal amount,
        string sourceSystem,
        float confidenceScore,
        string[] riskFlags)
        : base(originId)
    {
        OriginId = originId;
        Plate = plate;
        InfringementCode = infringementCode;
        Amount = amount;
        SourceSystem = sourceSystem;
        ConfidenceScore = confidenceScore;
        RiskFlags = riskFlags;
        ProcessedAt = DateTime.UtcNow;
    }

    public string OriginId { get; init; }
    public string Plate { get; init; }
    public int InfringementCode { get; init; }
    public decimal Amount { get; init; }
    public string SourceSystem { get; init; }
    public float ConfidenceScore { get; init; }
    public string[] RiskFlags { get; init; }
    public DateTime ProcessedAt { get; init; }
}