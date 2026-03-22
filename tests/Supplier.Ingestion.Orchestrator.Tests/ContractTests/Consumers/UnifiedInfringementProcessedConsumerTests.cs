using FluentAssertions;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using System.Text.Json;

namespace Supplier.Ingestion.Orchestrator.Tests.ContractTests.Consumers;

/// <summary>
/// Testes de contrato do consumidor para UnifiedInfringementProcessed.
/// Verificam que o consumidor consegue desserializar e processar mensagens
/// no formato definido pelo produtor (supplier-ingestion-orchestrator).
///
/// Nota: usa System.Text.Json diretamente por incompatibilidade do PactNet 5.0
/// com o runtime .NET 10 (FFI retorna corpo vazio em mensagens).
/// </summary>
public class UnifiedInfringementProcessedConsumerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void Should_Deserialize_ValidProcessedInfringement_FromSupplierA()
    {
        var json = """
            {
                "CorrelationId": "2ed6657d-e927-568b-95e1-2665a8aea6a2",
                "OriginId": "EXT-001",
                "Plate": "ABC1D23",
                "InfringementCode": 550,
                "Amount": 195.23,
                "SourceSystem": "SupplierA",
                "ProcessedAt": "2026-01-01T00:00:00Z"
            }
            """;

        var msg = JsonSerializer.Deserialize<UnifiedInfringementProcessed>(json, JsonOptions);

        msg.Should().NotBeNull();
        msg!.OriginId.Should().Be("EXT-001");
        msg.Plate.Should().Be("ABC1D23");
        msg.InfringementCode.Should().Be(550);
        msg.Amount.Should().Be(195.23m);
        msg.SourceSystem.Should().Be("SupplierA");
    }

    [Fact]
    public void Should_Deserialize_ValidProcessedInfringement_WithMercosulPlate()
    {
        var json = """
            {
                "CorrelationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                "OriginId": "EXT-002",
                "Plate": "ABC1D23",
                "InfringementCode": 600,
                "Amount": 293.47,
                "SourceSystem": "SupplierB",
                "ProcessedAt": "2026-01-01T00:00:00Z"
            }
            """;

        var msg = JsonSerializer.Deserialize<UnifiedInfringementProcessed>(json, JsonOptions);

        msg.Should().NotBeNull();
        msg!.Plate.Should().MatchRegex(@"[A-Z]{3}[0-9][A-Z][0-9]{2}");
        msg.Amount.Should().BeGreaterThanOrEqualTo(195m);
        msg.SourceSystem.Should().Be("SupplierB");
    }
}
