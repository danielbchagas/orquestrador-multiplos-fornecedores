using AutoFixture;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.StateMachines;
using System.Diagnostics;
using Testcontainers.Kafka;
using Xunit.Abstractions;

namespace Supplier.Ingestion.Orchestrator.Tests.LoadTests.Infrastructure;

public class SupplierAStateMachineLoadTests : IAsyncLifetime
{
    private readonly IFixture _fixture = new Fixture();
    private readonly ITestOutputHelper _output;

    private const int MESSAGE_COUNT = 5000; // Quantidade de sagas para testar
    private const int CONCURRENCY_LIMIT = 50; // Quantas sagas processam em paralelo
    private const int MAX_WAIT_SECONDS = 60; // Timeout de segurança

    private readonly KafkaContainer _kafkaContainer = new KafkaBuilder()
        .WithImage("confluentinc/cp-kafka:7.5.0")
        .Build();

    public SupplierAStateMachineLoadTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        await _kafkaContainer.StartAsync();

        // Setup inicial dos tópicos (igual ao seu teste original)
        var config = new AdminClientConfig { BootstrapServers = _kafkaContainer.GetBootstrapAddress() };
        using var adminClient = new AdminClientBuilder(config).Build();

        try
        {
            await adminClient.CreateTopicsAsync(new[]
            {
                new TopicSpecification { Name = "source.fornecedor-a.v1", NumPartitions = 4, ReplicationFactor = 1 }, // Mais partições para performance
                new TopicSpecification { Name = "target.dados.processados.v1", NumPartitions = 1, ReplicationFactor = 1 },
                new TopicSpecification { Name = "target.dados.invalidos.v1", NumPartitions = 1, ReplicationFactor = 1 }
            });
        }
        catch (CreateTopicsException e)
        {
            if (e.Results.Any(r => r.Error.Code != ErrorCode.TopicAlreadyExists)) throw;
        }
    }

    public async Task DisposeAsync() => await _kafkaContainer.DisposeAsync();

    [Fact]
    public async Task Deve_Suportar_Alta_Carga_De_Processamento()
    {
        // Arrange
        var topicInput = "source.fornecedor-a.v1";
        var consumerGroup = "saga-orchestrator-load-group";

        await using var provider = new ServiceCollection()
            .AddLogging(l => l.SetMinimumLevel(LogLevel.Warning))
            .AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<SupplierAStateMachine, SupplierState>()
                 .InMemoryRepository();

                x.AddRider(rider =>
                {
                    rider.AddProducer<string, UnifiedInfringementProcessed>("target.dados.processados.v1");
                    rider.AddProducer<string, InfringementValidationFailed>("target.dados.invalidos.v1");
                    rider.AddProducer<string, SupplierAInputReceived>(topicInput);

                    rider.UsingKafka((context, k) =>
                    {
                        k.Host(_kafkaContainer.GetBootstrapAddress());

                        k.TopicEndpoint<SupplierAInputReceived>(topicInput, consumerGroup, e =>
                        {
                            e.ConcurrentMessageLimit = CONCURRENCY_LIMIT;
                            e.PrefetchCount = CONCURRENCY_LIMIT * 2;
                            e.AutoOffsetReset = AutoOffsetReset.Earliest;

                            var stateMachine = context.GetRequiredService<SupplierAStateMachine>();
                            var repository = context.GetRequiredService<ISagaRepository<SupplierState>>();

                            e.StateMachineSaga(stateMachine, repository);
                        });
                    });
                });
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<SupplierAStateMachine, SupplierState>();

        var inputMessages = _fixture.Build<SupplierAInputReceived>()
            .With(x => x.TotalValue, 150.00m)
            .CreateMany(MESSAGE_COUNT)
            .ToList();

        _output.WriteLine($"Iniciando carga de {MESSAGE_COUNT} mensagens...");

        var stopwatch = Stopwatch.StartNew();

        // Act
        await Parallel.ForEachAsync(inputMessages, new ParallelOptions { MaxDegreeOfParallelism = 10 }, async (msg, ct) =>
        {
            await harness.Bus.Publish(msg, ct);
        });

        while (sagaHarness.Sagas.Count() < MESSAGE_COUNT)
        {
            if (stopwatch.Elapsed.TotalSeconds > MAX_WAIT_SECONDS)
                break;

            await Task.Delay(100);
        }

        stopwatch.Stop();

        // Assert & Metrics
        var processedCount = sagaHarness.Sagas.Count();
        var throughput = processedCount / stopwatch.Elapsed.TotalSeconds;

        _output.WriteLine($"---------------------------------------------------");
        _output.WriteLine($"Tempo Total: {stopwatch.Elapsed.TotalSeconds:F2}s");
        _output.WriteLine($"Processados: {processedCount}/{MESSAGE_COUNT}");
        _output.WriteLine($"Throughput:  {throughput:F2} sagas/segundo");
        _output.WriteLine($"---------------------------------------------------");

        processedCount.Should().Be(MESSAGE_COUNT);
    }
}