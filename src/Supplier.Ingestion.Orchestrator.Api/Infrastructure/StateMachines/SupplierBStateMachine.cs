using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;

namespace Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;

public class SupplierBStateMachine : SupplierStateMachineBase<SupplierBInputReceived>
{
    public SupplierBStateMachine(ILogger<SupplierBStateMachine> logger)
        : base(logger, "B") { }
}
