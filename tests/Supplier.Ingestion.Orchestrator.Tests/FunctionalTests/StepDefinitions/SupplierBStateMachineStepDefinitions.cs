using Reqnroll;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;

namespace Supplier.Ingestion.Orchestrator.Tests.FunctionalTests.StepDefinitions;

[Binding]
[Scope(Feature = "Supplier B State Machine")]
public class SupplierBStateMachineStepDefinitions
    : SupplierStateMachineStepDefinitionsBase<SupplierBStateMachine, SupplierBState, SupplierBInputReceived>
{
    [Given(@"a valid infringement event from Supplier B with plate ""(.*)"" and amount (.*)")]
    public void GivenAValidInfringementEventFromSupplierB(string plate, decimal amount)
    {
        CorrelationId = Guid.NewGuid();
        InputEvent = new SupplierBInputReceived($"EXT-{CorrelationId}", plate, 5678, amount)
        {
            CorrelationId = CorrelationId
        };
    }

    [Given(@"an invalid infringement event from Supplier B with plate ""(.*)"" and amount (.*)")]
    public void GivenAnInvalidInfringementEventFromSupplierB(string plate, decimal amount)
    {
        CorrelationId = Guid.NewGuid();
        InputEvent = new SupplierBInputReceived($"EXT-{CorrelationId}", plate, 5678, amount)
        {
            CorrelationId = CorrelationId
        };
    }
}
