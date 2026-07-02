using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Persistence;

/// <summary>
/// Estado do processamento de uma infração — equivalente ao <c>SupplierState</c> (saga)
/// da versão MassTransit. O documento é inserido com o CorrelationId determinístico como _id,
/// o que serve de barreira de idempotência para mensagens duplicadas.
/// </summary>
public class SupplierIngestionState
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid CorrelationId { get; set; }

    public string CurrentState { get; set; } = "Processing";

    public string ExternalId { get; set; } = string.Empty;
    public string Plate { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string OriginSystem { get; set; } = string.Empty;
    public int InfringementCode { get; set; }

    public bool IsValid { get; set; }

    public string ValidationErrors { get; set; } = string.Empty;

    public string? AiAnalysis { get; set; }
    public bool? AiIsSuspicious { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
