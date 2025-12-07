using MassTransit;
using MongoDB.Bson.Serialization.Attributes;

namespace Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;

public class SupplierState : SagaStateMachineInstance, ISagaVersion
{
    [BsonId]
    public Guid CorrelationId { get; set; }

    public int Version { get; set; }
    public string CurrentState { get; set; }

    public string ExternalId { get; set; }
    public string Plate { get; set; }
    public decimal Amount { get; set; }
    public string OriginSystem { get; set; }
    public int InfringementCode { get; set; }

    public bool IsValid { get; set; }

    public string ValidationErrors { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
