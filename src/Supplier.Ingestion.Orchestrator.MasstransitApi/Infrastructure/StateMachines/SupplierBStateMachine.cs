using Supplier.Ingestion.Orchestrator.MasstransitApi.Infrastructure.Events;

namespace Supplier.Ingestion.Orchestrator.MasstransitApi.Infrastructure.StateMachines;

public class SupplierBStateMachine : SupplierStateMachineBase<SupplierBInputReceived>
{
    public SupplierBStateMachine(ILogger<SupplierBStateMachine> logger)
        : base(logger, "B") { }
}
