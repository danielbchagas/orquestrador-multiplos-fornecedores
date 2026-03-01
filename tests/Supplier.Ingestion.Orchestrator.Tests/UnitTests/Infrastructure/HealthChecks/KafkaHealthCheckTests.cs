using Confluent.Kafka;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.HealthChecks;

namespace Supplier.Ingestion.Orchestrator.Tests.UnitTests.Infrastructure.HealthChecks;

public class KafkaHealthCheckTests
{
    private readonly HealthCheckContext _context = new();

    [Fact]
    public async Task CheckHealthAsync_WhenKafkaHasBrokers_ReturnsHealthy()
    {
        // Arrange
        var mockAdminClient = new Mock<IAdminClient>();
        mockAdminClient
            .Setup(c => c.GetMetadata(It.IsAny<TimeSpan>()))
            .Returns(CreateMetadataWithBrokers(2));

        var healthCheck = new KafkaHealthCheck(() => mockAdminClient.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(_context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("Brokers: 2");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenKafkaReturnNoBrokers_ReturnsDegraded()
    {
        // Arrange
        var mockAdminClient = new Mock<IAdminClient>();
        mockAdminClient
            .Setup(c => c.GetMetadata(It.IsAny<TimeSpan>()))
            .Returns(CreateMetadataWithBrokers(0));

        var healthCheck = new KafkaHealthCheck(() => mockAdminClient.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(_context);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Be("Kafka returned no broker metadata.");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenKafkaThrows_ReturnsUnhealthy()
    {
        // Arrange
        var exception = new Exception("Broker unreachable");

        var mockAdminClient = new Mock<IAdminClient>();
        mockAdminClient
            .Setup(c => c.GetMetadata(It.IsAny<TimeSpan>()))
            .Throws(exception);

        var healthCheck = new KafkaHealthCheck(() => mockAdminClient.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(_context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Exception.Should().Be(exception);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenFactoryThrows_ReturnsUnhealthy()
    {
        // Arrange
        var exception = new InvalidOperationException("Cannot create admin client");

        var healthCheck = new KafkaHealthCheck(() => throw exception);

        // Act
        var result = await healthCheck.CheckHealthAsync(_context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Exception.Should().Be(exception);
    }

    private static Metadata CreateMetadataWithBrokers(int brokerCount)
    {
        var brokers = Enumerable
            .Range(1, brokerCount)
            .Select(i => new BrokerMetadata(i, $"broker-{i}", 9092))
            .ToList();

        return new Metadata(brokers, [], 1, "test-cluster");
    }
}
