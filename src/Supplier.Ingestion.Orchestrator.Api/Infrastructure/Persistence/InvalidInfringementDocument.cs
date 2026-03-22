using MongoDB.Bson.Serialization.Attributes;

namespace Supplier.Ingestion.Orchestrator.Api.Infrastructure.Persistence;

public class InvalidInfringementDocument
{
    [BsonId]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CorrelationId { get; set; }
    public string OriginId { get; set; } = string.Empty;
    public string OriginSystem { get; set; } = string.Empty;
    public string Plate { get; set; } = string.Empty;
    public int InfringementCode { get; set; }
    public decimal Amount { get; set; }
    public string FailureReason { get; set; } = string.Empty;
    public DateTime FailedAt { get; set; }
    public int RetryCount { get; set; }
    public DateTime? LastRetryAt { get; set; }
}
