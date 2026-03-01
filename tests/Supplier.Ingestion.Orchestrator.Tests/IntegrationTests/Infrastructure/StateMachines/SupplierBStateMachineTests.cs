using AutoFixture;
using Confluent.Kafka;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;

namespace Supplier.Ingestion.Orchestrator.Tests.IntegrationTests.Infrastructure.StateMachines;

[Collection("SequentialIntegrationTests")]
public class SupplierBStateMachineTests
    : SupplierStateMachineIntegrationTestsBase<SupplierBStateMachine, SupplierState, SupplierBInputReceived>
{
    protected override string InputTopic    => "integration-source.supplier-b.v1";
    protected override string ConsumerGroup => "integration-saga-orchestrator-test-group-b";

    protected override SupplierBInputReceived BuildValidInputEvent() =>
        Fixture.Build<SupplierBInputReceived>()
            .With(x => x.TotalValue, 355.50m)
            .Create();

    protected override void RegisterTopicEndpoint(IRiderRegistrationContext context, IKafkaFactoryConfigurator k)
    {
        k.TopicEndpoint<SupplierBInputReceived>(InputTopic, ConsumerGroup, e =>
        {
            e.AutoOffsetReset = AutoOffsetReset.Earliest;
            e.StateMachineSaga(
                context.GetRequiredService<SupplierBStateMachine>(),
                context.GetRequiredService<ISagaRepository<SupplierState>>());
        });
    }

    protected override (string ExternalId, string Plate, int InfringementCode, decimal Amount, string OriginSystem)
        ProjectSagaFields(SupplierBInputReceived e) =>
            (e.ExternalCode, e.Plate, e.Infringement, e.TotalValue, e.OriginSystem);
}
