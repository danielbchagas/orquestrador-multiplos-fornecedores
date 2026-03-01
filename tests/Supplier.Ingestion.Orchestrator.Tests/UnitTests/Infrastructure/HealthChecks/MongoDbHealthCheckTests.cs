using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.HealthChecks;

namespace Supplier.Ingestion.Orchestrator.Tests.UnitTests.Infrastructure.HealthChecks;

public class MongoDbHealthCheckTests
{
    private readonly HealthCheckContext _context = new();

    [Fact]
    public async Task CheckHealthAsync_WhenMongoIsReachable_ReturnsHealthy()
    {
        // Arrange
        var mockDatabase = new Mock<IMongoDatabase>();
        mockDatabase
            .Setup(db => db.RunCommandAsync<BsonDocument>(
                It.IsAny<Command<BsonDocument>>(),
                It.IsAny<ReadPreference?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BsonDocument("ok", 1));

        var mockClient = new Mock<IMongoClient>();
        mockClient
            .Setup(c => c.GetDatabase("admin", null))
            .Returns(mockDatabase.Object);

        var healthCheck = new MongoDbHealthCheck(mockClient.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(_context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("MongoDB is reachable.");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenMongoThrows_ReturnsUnhealthy()
    {
        // Arrange
        var exception = new Exception("Connection refused");

        var mockDatabase = new Mock<IMongoDatabase>();
        mockDatabase
            .Setup(db => db.RunCommandAsync<BsonDocument>(
                It.IsAny<Command<BsonDocument>>(),
                It.IsAny<ReadPreference?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var mockClient = new Mock<IMongoClient>();
        mockClient
            .Setup(c => c.GetDatabase("admin", null))
            .Returns(mockDatabase.Object);

        var healthCheck = new MongoDbHealthCheck(mockClient.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(_context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Exception.Should().Be(exception);
    }
}
