using Supplier.Ingestion.Orchestrator.MasstransitApi.Infrastructure.Events;

namespace Supplier.Ingestion.Orchestrator.MasstransitApi.Infrastructure.StateMachines;

public class SupplierAStateMachine : SupplierStateMachineBase<SupplierAInputReceived>
{
    public SupplierAStateMachine(ILogger<SupplierAStateMachine> logger)
        : base(logger, "A") { }
}
