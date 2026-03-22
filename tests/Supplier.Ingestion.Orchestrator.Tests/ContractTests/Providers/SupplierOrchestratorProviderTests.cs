using FluentAssertions;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using System.Text.Json;

namespace Supplier.Ingestion.Orchestrator.Tests.ContractTests.Providers;

/// <summary>
/// Testes de contrato do produtor (supplier-ingestion-orchestrator).
/// Verificam que os eventos produzidos pelo orquestrador podem ser serializados
/// para JSON com todos os campos exigidos pelos consumidores.
/// </summary>
public class SupplierOrchestratorProviderTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void Should_Produce_UnifiedInfringementProcessed_WithAllRequiredFields()
    {
        var evt = new UnifiedInfringementProcessed("EXT-001", "ABC1D23", 550, 195.23m, "SupplierA");

        var json = JsonSerializer.Serialize(evt, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);

        deserialized.GetProperty("OriginId").GetString().Should().Be("EXT-001");
        deserialized.GetProperty("Plate").GetString().Should().Be("ABC1D23");
        deserialized.GetProperty("InfringementCode").GetInt32().Should().Be(550);
        deserialized.GetProperty("Amount").GetDecimal().Should().Be(195.23m);
        deserialized.GetProperty("SourceSystem").GetString().Should().Be("SupplierA");
        deserialized.GetProperty("ProcessedAt").GetDateTime().Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        deserialized.GetProperty("CorrelationId").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public void Should_Produce_UnifiedInfringementProcessed_WithDeterministicCorrelationId()
    {
        var evt1 = new UnifiedInfringementProcessed("EXT-001", "ABC1D23", 550, 195.23m, "SupplierA");
        var evt2 = new UnifiedInfringementProcessed("EXT-001", "DEF9G87", 600, 293.47m, "SupplierB");

        evt1.CorrelationId.Should().Be(evt2.CorrelationId, "mesmo OriginId deve gerar mesmo CorrelationId (UUID v5)");
    }

    [Fact]
    public void Should_Produce_InfringementValidationFailed_WithAllRequiredFields()
    {
        var evt = new InfringementValidationFailed("EXT-003", "SupplierA", "", 550, 0m, "Placa não pode ser vazia");

        var json = JsonSerializer.Serialize(evt, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);

        deserialized.GetProperty("OriginId").GetString().Should().Be("EXT-003");
        deserialized.GetProperty("OriginSystem").GetString().Should().Be("SupplierA");
        deserialized.GetProperty("Plate").GetString().Should().BeEmpty();
        deserialized.GetProperty("InfringementCode").GetInt32().Should().Be(550);
        deserialized.GetProperty("FailureReason").GetString().Should().NotBeNullOrEmpty();
        deserialized.GetProperty("FailedAt").GetDateTime().Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        deserialized.GetProperty("CorrelationId").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public void Should_Produce_InfringementValidationFailed_WithAiReason_ContainingAllOriginalData()
    {
        var evt = new InfringementValidationFailed("EXT-004", "SupplierB", "ABC1D23", 550, 195.23m, "AI: placa suspeita identificada");

        evt.Plate.Should().Be("ABC1D23");
        evt.InfringementCode.Should().Be(550);
        evt.Amount.Should().Be(195.23m);
        evt.FailureReason.Should().StartWith("AI:");
    }
}
