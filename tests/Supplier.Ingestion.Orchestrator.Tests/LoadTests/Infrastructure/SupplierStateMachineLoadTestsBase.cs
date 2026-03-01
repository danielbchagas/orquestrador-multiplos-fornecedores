using AutoFixture;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBomber.CSharp;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;
using System.Diagnostics;
using Testcontainers.Kafka;
using Xunit.Abstractions;

namespace Supplier.Ingestion.Orchestrator.Tests.LoadTests.Infrastructure;

public abstract class SupplierStateMachineLoadTestsBase<TStateMachine, TState, TInputEvent> : IAsyncLifetime
    where TStateMachine : class, MassTransit.SagaStateMachine<TState>
    where TState : SupplierState, new()
    where TInputEvent : class, MassTransit.CorrelatedBy<Guid>
{
    protected readonly IFixture Fixture = new Fixture();
    private readonly ITestOutputHelper _output;

    private const int ConcurrencyLimit    = 50;
    private const int LoadRate            = 20;
    private const int LoadDurationSeconds = 10;
    private const int DrainTimeoutSeconds = 60;

    // Único por subclasse, evita colisão de nome quando o xUnit roda as classes em paralelo.
    private string ScenarioName => $"ingestao_kafka_{typeof(TStateMachine).Name.ToLowerInvariant()}";

    private readonly KafkaContainer _kafkaContainer = new KafkaBuilder()
        .WithImage("confluentinc/cp-kafka:7.5.0")
        .Build();

    protected abstract string InputTopic    { get; }
    protected abstract string ConsumerGroup { get; }

    private const string TopicSuccess = "load-target.processed.data.v1";
    private const string TopicError   = "load-target.invalid.data.v1";

    protected SupplierStateMachineLoadTestsBase(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Constrói o evento de entrada a ser publicado em cada iteração do NBomber.
    /// TotalValue deve ser positivo para garantir que a saga seja processada com sucesso.
    /// </summary>
    protected abstract TInputEvent BuildInputEvent();

    public async Task InitializeAsync()
    {
        await _kafkaContainer.StartAsync();

        using var adminClient = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = _kafkaContainer.GetBootstrapAddress()
        }).Build();

        try
        {
            await adminClient.CreateTopicsAsync(new[]
            {
                new TopicSpecification { Name = InputTopic,   NumPartitions = 4, ReplicationFactor = 1 },
                new TopicSpecification { Name = TopicSuccess, NumPartitions = 1, ReplicationFactor = 1 },
                new TopicSpecification { Name = TopicError,   NumPartitions = 1, ReplicationFactor = 1 }
            });
        }
        catch (CreateTopicsException e)
        {
            if (e.Results.Any(r => r.Error.Code != ErrorCode.TopicAlreadyExists))
                throw;
        }
    }

    public async Task DisposeAsync() => await _kafkaContainer.DisposeAsync();

    [Fact]
    public async Task Should_Support_High_Load()
    {
        // Arrange
        await using var provider = new ServiceCollection()
            .AddLogging(l => l.SetMinimumLevel(LogLevel.Error))
            .AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<TStateMachine, TState>()
                 .InMemoryRepository();

                x.AddRider(rider =>
                {
                    rider.AddProducer<string, UnifiedInfringementProcessed>(TopicSuccess);
                    rider.AddProducer<string, InfringementValidationFailed>(TopicError);
                    rider.AddProducer<string, TInputEvent>(InputTopic);

                    rider.UsingKafka((context, k) =>
                    {
                        k.Host(_kafkaContainer.GetBootstrapAddress());

                        k.TopicEndpoint<TInputEvent>(InputTopic, ConsumerGroup, e =>
                        {
                            e.ConcurrentMessageLimit = ConcurrencyLimit;
                            e.PrefetchCount          = ConcurrencyLimit * 2;
                            e.AutoOffsetReset        = AutoOffsetReset.Earliest;

                            e.StateMachineSaga(
                                context.GetRequiredService<TStateMachine>(),
                                context.GetRequiredService<ISagaRepository<TState>>());
                        });
                    });
                });
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var scenarioName = ScenarioName;

        var scenario = Scenario.Create(scenarioName, async context =>
        {
            try
            {
                await harness.Bus.Publish(BuildInputEvent(), context.ScenarioCancellationToken);
                return NBomber.CSharp.Response.Ok();
            }
            catch
            {
                return NBomber.CSharp.Response.Fail();
            }
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(
                rate:     LoadRate,
                interval: TimeSpan.FromSeconds(1),
                during:   TimeSpan.FromSeconds(LoadDurationSeconds))
        );

        _output.WriteLine("=== Starting NBomber (Load Simulation) ===");

        var stats         = NBomberRunner.RegisterScenarios(scenario).Run();
        var scenarioStats = stats.ScenarioStats.First(s => s.ScenarioName == scenarioName);
        var totalSent     = scenarioStats.AllOkCount;

        var sagaHarness = harness.GetSagaStateMachineHarness<TStateMachine, TState>();

        _output.WriteLine($"[NBomber] Messages sent successfully: {totalSent}");
        _output.WriteLine($"[NBomber] Send failures: {scenarioStats.AllFailCount}");
        _output.WriteLine("[MassTransit] Waiting for consumer to drain the queue...");

        // Act — drain
        var stopwatch = Stopwatch.StartNew();

        while (sagaHarness.Sagas.Count() < totalSent)
        {
            if (stopwatch.Elapsed.TotalSeconds > DrainTimeoutSeconds)
            {
                _output.WriteLine("TIMEOUT: Consumer could not process all messages in time.");
                break;
            }

            await Task.Delay(500);
        }

        stopwatch.Stop();

        var processed  = sagaHarness.Sagas.Count();
        var throughput = processed / stopwatch.Elapsed.TotalSeconds;

        _output.WriteLine($"---------------------------------------------------");
        _output.WriteLine($"Drain Time: {stopwatch.Elapsed.TotalSeconds:F2}s");
        _output.WriteLine($"Sagas Processed: {processed}/{totalSent}");
        _output.WriteLine($"Final Throughput: {throughput:F2} sagas/second");
        _output.WriteLine($"---------------------------------------------------");

        // Assert
        Assert.True(scenarioStats.AllFailCount == 0, "There were errors sending (Producer) to Kafka.");
        Assert.True(totalSent > 0, "No messages were sent by NBomber.");

        processed.Should().Be(totalSent,
            "the number processed by the Consumer should equal the number sent by NBomber.");
    }
}
