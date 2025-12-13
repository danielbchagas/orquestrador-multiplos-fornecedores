using AutoFixture;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBomber.CSharp;
using NBomber.Contracts;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;
using System.Diagnostics;
using Testcontainers.Kafka;
using Xunit.Abstractions;

namespace Supplier.Ingestion.Orchestrator.Tests.LoadTests.Infrastructure;

public class SupplierBStateMachineTests : IAsyncLifetime
{
    private readonly IFixture _fixture = new Fixture();
    private readonly ITestOutputHelper _output;

    private const int CONCURRENCY_LIMIT = 50;
    private const string SCENARIO_NAME = "ingestao_kafka";

    private const int LOAD_RATE = 20;
    private const int LOAD_DURATION_SECONDS = 10;

    private readonly KafkaContainer _kafkaContainer = new KafkaBuilder()
        .WithImage("confluentinc/cp-kafka:7.5.0")
        .Build();

    public SupplierBStateMachineTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        await _kafkaContainer.StartAsync();

        var config = new AdminClientConfig { BootstrapServers = _kafkaContainer.GetBootstrapAddress() };
        using var adminClient = new AdminClientBuilder(config).Build();

        try
        {
            await adminClient.CreateTopicsAsync(new[]
            {
                new TopicSpecification { Name = "load-source.fornecedor-b.v1", NumPartitions = 4, ReplicationFactor = 1 },
                new TopicSpecification { Name = "load-target.dados.processados.v1", NumPartitions = 1, ReplicationFactor = 1 },
                new TopicSpecification { Name = "load-target.dados.invalidos.v1", NumPartitions = 1, ReplicationFactor = 1 }
            });
        }
        catch (CreateTopicsException e)
        {
            if (e.Results.Any(r => r.Error.Code != ErrorCode.TopicAlreadyExists)) throw;
        }
    }

    public async Task DisposeAsync() => await _kafkaContainer.DisposeAsync();

    [Fact]
    public async Task Should_Support_High_Load()
    {
        // Arrange
        var topicInput = "load-source.fornecedor-b.v1";

        await using var provider = new ServiceCollection()
            .AddLogging(l => l.SetMinimumLevel(LogLevel.Error))
            .AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<SupplierBStateMachine, SupplierState>()
                 .InMemoryRepository();

                x.AddRider(rider =>
                {
                    rider.AddProducer<string, UnifiedInfringementProcessed>("load-target.dados.processados.v1");
                    rider.AddProducer<string, InfringementValidationFailed>("load-target.dados.invalidos.v1");

                    rider.AddProducer<string, SupplierBInputReceived>(topicInput);

                    rider.UsingKafka((context, k) =>
                    {
                        k.Host(_kafkaContainer.GetBootstrapAddress());

                        k.TopicEndpoint<SupplierBInputReceived>(topicInput, "load-nbomber-group", e =>
                        {
                            e.ConcurrentMessageLimit = CONCURRENCY_LIMIT;
                            e.PrefetchCount = CONCURRENCY_LIMIT * 2;
                            e.AutoOffsetReset = AutoOffsetReset.Earliest;

                            e.StateMachineSaga(context.GetRequiredService<SupplierBStateMachine>(),
                                               context.GetRequiredService<ISagaRepository<SupplierState>>());
                        });
                    });
                });
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var scenario = Scenario.Create(SCENARIO_NAME, async context =>
        {
            var msg = _fixture.Build<SupplierBInputReceived>()
                .With(x => x.TotalValue, 150.00m)
                .Create();

            try
            {
                await harness.Bus.Publish(msg, context.ScenarioCancellationToken);

                return NBomber.CSharp.Response.Ok();
            }
            catch (Exception ex)
            {
                return NBomber.CSharp.Response.Fail();
            }
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(rate: LOAD_RATE,
                              interval: TimeSpan.FromSeconds(1),
                              during: TimeSpan.FromSeconds(LOAD_DURATION_SECONDS))
        );

        _output.WriteLine("=== Iniciando NBomber (Simulação de Carga) ===");

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        var scenarioStats = stats.ScenarioStats.Get(SCENARIO_NAME);

        var totalEnviado = scenarioStats.AllOkCount;

        var sagaHarness = harness.GetSagaStateMachineHarness<SupplierBStateMachine, SupplierState>();

        _output.WriteLine($"[NBomber] Mensagens enviadas com sucesso: {totalEnviado}");
        _output.WriteLine($"[NBomber] Falhas no envio: {scenarioStats.AllFailCount}");
        _output.WriteLine("[MassTransit] Aguardando consumer drenar a fila...");

        var stopwatch = Stopwatch.StartNew();

        while (sagaHarness.Sagas.Count() < totalEnviado)
        {
            if (stopwatch.Elapsed.TotalSeconds > 60)
            {
                _output.WriteLine("TIMEOUT: Consumer não conseguiu processar tudo a tempo.");
                break;
            }
            await Task.Delay(500);
        }
        stopwatch.Stop();

        var processados = sagaHarness.Sagas.Count();
        var throughput = processados / stopwatch.Elapsed.TotalSeconds;

        _output.WriteLine($"---------------------------------------------------");
        _output.WriteLine($"Tempo de Drenagem: {stopwatch.Elapsed.TotalSeconds:F2}s");
        _output.WriteLine($"Sagas Processadas: {processados}/{totalEnviado}");
        _output.WriteLine($"Throughput Final:  {throughput:F2} sagas/segundo");
        _output.WriteLine($"---------------------------------------------------");

        // Asserts
        Assert.True(scenarioStats.AllFailCount == 0, "Houve erros no envio (Producer) para o Kafka.");
        Assert.True(totalEnviado > 0, "Nenhuma mensagem foi enviada pelo NBomber.");

        processados.Should().Be(totalEnviado,
            "A quantidade processada pelo Consumer deve ser igual à enviada pelo NBomber.");
    }
}
