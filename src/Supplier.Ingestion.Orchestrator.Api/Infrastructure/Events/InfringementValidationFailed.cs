namespace Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;

public record InfringementValidationFailed : IntegrationEvent
{
    public InfringementValidationFailed(
        string originId,
        string originSystem,
        string reason)
        : base(originId)
    {
        OriginId = originId;
        OriginSystem = originSystem;
        FailureReason = reason;
        FailedAt = DateTime.UtcNow;
    }

    public string OriginId { get; init; }
    public string OriginSystem { get; init; }
    public string FailureReason { get; init; }
    public DateTime FailedAt { get; init; }
}