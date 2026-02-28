using Reqnroll;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;

namespace Supplier.Ingestion.Orchestrator.Tests.FunctionalTests.StepDefinitions;

[Binding]
[Scope(Feature = "Supplier A State Machine")]
public class SupplierAStateMachineStepDefinitions
    : SupplierStateMachineStepDefinitionsBase<SupplierAStateMachine, SupplierAInputReceived>
{
    [Given(@"a valid infringement event from Supplier A with plate ""(.*)"" and amount (.*)")]
    public void GivenAValidInfringementEventFromSupplierA(string plate, decimal amount)
    {
        CorrelationId = Guid.NewGuid();
        InputEvent = new SupplierAInputReceived($"EXT-{CorrelationId}", plate, 1234, amount)
        {
            CorrelationId = CorrelationId
        };
    }

    [Given(@"an invalid infringement event from Supplier A with plate ""(.*)"" and amount (.*)")]
    public void GivenAnInvalidInfringementEventFromSupplierA(string plate, decimal amount)
    {
        CorrelationId = Guid.NewGuid();
        InputEvent = new SupplierAInputReceived($"EXT-{CorrelationId}", plate, 1234, amount)
        {
            CorrelationId = CorrelationId
        };
    }
}
