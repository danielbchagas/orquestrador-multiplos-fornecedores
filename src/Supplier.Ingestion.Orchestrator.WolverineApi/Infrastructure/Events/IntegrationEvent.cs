namespace Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Events;

public abstract record IntegrationEvent
{
    public Guid CorrelationId { get; init; }

    protected IntegrationEvent(string businessKey)
    {
        CorrelationId = DeterministicId.FromBusinessKey(businessKey);
    }
}
