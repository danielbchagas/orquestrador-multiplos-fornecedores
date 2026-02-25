using AutoFixture;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;
using Xunit.Abstractions;

namespace Supplier.Ingestion.Orchestrator.Tests.LoadTests.Infrastructure;

[Collection("SequentialIntegrationTests")]
public class SupplierBStateMachineTests
    : SupplierStateMachineLoadTestsBase<SupplierBStateMachine, SupplierBInputReceived>
{
    public SupplierBStateMachineTests(ITestOutputHelper output) : base(output) { }

    protected override string InputTopic    => "load-source.supplier-b.v1";
    protected override string ConsumerGroup => "load-nbomber-group-b";

    protected override SupplierBInputReceived BuildInputEvent() =>
        new(
            externalCode: Fixture.Create<string>(),
            plate:        Fixture.Create<string>(),
            infringement: Fixture.Create<int>(),
            totalValue:   355.50m
        );
}
