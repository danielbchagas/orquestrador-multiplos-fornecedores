using FluentAssertions;
using Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;
using System.Text.Json;

namespace Supplier.Ingestion.Orchestrator.Tests.ContractTests.Consumers;

/// <summary>
/// Testes de contrato do consumidor para InfringementValidationFailed.
/// Verificam que o consumidor (dlq-consumer) consegue desserializar e processar
/// mensagens de falha no formato definido pelo produtor.
/// </summary>
public class InfringementValidationFailedConsumerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void Should_Deserialize_FailedValidationEvent_WithBasicValidationReason()
    {
        var json = """
            {
                "CorrelationId": "2ed6657d-e927-568b-95e1-2665a8aea6a2",
                "OriginId": "EXT-003",
                "OriginSystem": "SupplierA",
                "Plate": "",
                "InfringementCode": 550,
                "Amount": 0.0,
                "FailureReason": "Placa não pode ser vazia",
                "FailedAt": "2026-01-01T00:00:00Z"
            }
            """;

        var msg = JsonSerializer.Deserialize<InfringementValidationFailed>(json, JsonOptions);

        msg.Should().NotBeNull();
        msg!.OriginId.Should().Be("EXT-003");
        msg.OriginSystem.Should().Be("SupplierA");
        msg.FailureReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Should_Deserialize_FailedValidationEvent_WithAiReason()
    {
        var json = """
            {
                "CorrelationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                "OriginId": "EXT-004",
                "OriginSystem": "SupplierB",
                "Plate": "ABC1D23",
                "InfringementCode": 550,
                "Amount": 195.23,
                "FailureReason": "AI: placa suspeita identificada",
                "FailedAt": "2026-01-01T00:00:00Z"
            }
            """;

        var msg = JsonSerializer.Deserialize<InfringementValidationFailed>(json, JsonOptions);

        msg.Should().NotBeNull();
        msg!.OriginId.Should().Be("EXT-004");
        msg.FailureReason.Should().StartWith("AI:");
        msg.InfringementCode.Should().BeGreaterThan(0);
        msg.Amount.Should().BeGreaterThan(0);
    }
}
