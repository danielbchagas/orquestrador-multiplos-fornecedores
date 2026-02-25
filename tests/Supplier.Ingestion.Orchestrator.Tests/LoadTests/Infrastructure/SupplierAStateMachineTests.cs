using AutoFixture;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;
using Xunit.Abstractions;

namespace Supplier.Ingestion.Orchestrator.Tests.LoadTests.Infrastructure;

[Collection("SequentialIntegrationTests")]
public class SupplierAStateMachineTests
    : SupplierStateMachineLoadTestsBase<SupplierAStateMachine, SupplierAInputReceived>
{
    public SupplierAStateMachineTests(ITestOutputHelper output) : base(output) { }

    protected override string InputTopic    => "load-source.supplier-a.v1";
    protected override string ConsumerGroup => "load-nbomber-group-a";

    protected override SupplierAInputReceived BuildInputEvent() =>
        new(
            externalCode: Fixture.Create<string>(),
            plate:        Fixture.Create<string>(),
            infringement: Fixture.Create<int>(),
            totalValue:   150.00m
        );
}
