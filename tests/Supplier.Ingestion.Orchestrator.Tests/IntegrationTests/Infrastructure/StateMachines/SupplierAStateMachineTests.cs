using AutoFixture;
using Confluent.Kafka;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;

namespace Supplier.Ingestion.Orchestrator.Tests.IntegrationTests.Infrastructure.StateMachines;

[Collection("SequentialIntegrationTests")]
public class SupplierAStateMachineTests
    : SupplierStateMachineIntegrationTestsBase<SupplierAStateMachine, SupplierAInputReceived>
{
    protected override string InputTopic    => "integration-source.supplier-a.v1";
    protected override string ConsumerGroup => "integration-saga-orchestrator-test-group-a";

    protected override SupplierAInputReceived BuildValidInputEvent() =>
        Fixture.Build<SupplierAInputReceived>()
            .With(x => x.TotalValue, 150.00m)
            .Create();

    protected override void RegisterTopicEndpoint(IRiderRegistrationContext context, IKafkaFactoryConfigurator k)
    {
        k.TopicEndpoint<SupplierAInputReceived>(InputTopic, ConsumerGroup, e =>
        {
            e.AutoOffsetReset = AutoOffsetReset.Earliest;
            e.StateMachineSaga(
                context.GetRequiredService<SupplierAStateMachine>(),
                context.GetRequiredService<ISagaRepository<SupplierState>>());
        });
    }

    protected override (string ExternalId, string Plate, int InfringementCode, decimal Amount, string OriginSystem)
        ProjectSagaFields(SupplierAInputReceived e) =>
            (e.ExternalCode, e.Plate, e.Infringement, e.TotalValue, e.OriginSystem);
}
