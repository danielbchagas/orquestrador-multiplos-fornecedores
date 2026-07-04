using AutoFixture;
using Supplier.Ingestion.Orchestrator.MasstransitApi.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.MasstransitApi.Infrastructure.StateMachines;

namespace Supplier.Ingestion.Orchestrator.Tests.UnitTests.Infrastructure.StateMachines;

public class SupplierAStateMachineTests : SupplierStateMachineTestsBase<SupplierAStateMachine, SupplierAInputReceived>
{
    protected override SupplierAInputReceived BuildValidInputEvent(Guid correlationId) =>
        Fixture.Build<SupplierAInputReceived>()
            .With(x => x.CorrelationId, correlationId)
            .With(x => x.TotalValue, 100m)
            .Create();

    protected override SupplierAInputReceived BuildInvalidInputEvent(Guid correlationId) =>
        Fixture.Build<SupplierAInputReceived>()
            .With(x => x.CorrelationId, correlationId)
            .With(x => x.TotalValue, -1m)
            .Create();
}
