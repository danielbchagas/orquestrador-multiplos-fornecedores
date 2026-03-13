using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;

namespace Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;

public class SupplierAStateMachine : SupplierStateMachineBase<SupplierAInputReceived>
{
    public SupplierAStateMachine(ILogger<SupplierAStateMachine> logger)
        : base(logger, "A") { }
}
