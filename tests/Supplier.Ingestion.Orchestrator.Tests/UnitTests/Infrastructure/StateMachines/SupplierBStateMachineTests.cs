using AutoFixture;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;

namespace Supplier.Ingestion.Orchestrator.Tests.UnitTests.Infrastructure.StateMachines;

public class SupplierBStateMachineTests : SupplierStateMachineTestsBase<SupplierBStateMachine, SupplierBInputReceived>
{
    protected override SupplierBInputReceived BuildValidInputEvent(Guid correlationId) =>
        Fixture.Build<SupplierBInputReceived>()
            .With(x => x.CorrelationId, correlationId)
            .With(x => x.TotalValue, 150.75m)
            .Create();

    protected override SupplierBInputReceived BuildInvalidInputEvent(Guid correlationId) =>
        Fixture.Build<SupplierBInputReceived>()
            .With(x => x.CorrelationId, correlationId)
            .With(x => x.TotalValue, -1m)
            .Create();
}
