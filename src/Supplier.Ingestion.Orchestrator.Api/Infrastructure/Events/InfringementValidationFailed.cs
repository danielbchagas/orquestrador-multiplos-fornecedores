namespace Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;

public record InfringementValidationFailed : IntegrationEvent
{
    public InfringementValidationFailed(
        string originId,
        string originSystem,
        string plate,
        int infringementCode,
        decimal amount,
        string reason)
        : base(originId)
    {
        OriginId = originId;
        OriginSystem = originSystem;
        Plate = plate;
        InfringementCode = infringementCode;
        Amount = amount;
        FailureReason = reason;
        FailedAt = DateTime.UtcNow;
    }

    public string OriginId { get; init; }
    public string OriginSystem { get; init; }
    public string Plate { get; init; }
    public int InfringementCode { get; init; }
    public decimal Amount { get; init; }
    public string FailureReason { get; init; }
    public DateTime FailedAt { get; init; }
}