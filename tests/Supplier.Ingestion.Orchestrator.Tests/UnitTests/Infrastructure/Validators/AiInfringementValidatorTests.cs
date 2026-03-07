using FluentAssertions;
using Supplier.Ingestion.Orchestrator.Api.Validators;

namespace Supplier.Ingestion.Orchestrator.Tests.UnitTests.Infrastructure.Validators;

public class AiInfringementValidatorTests
{
    // --- ParseResponse tests ---

    [Fact]
    public void ParseResponse_WhenValidJsonReturned_ShouldMapAllFields()
    {
        var json = """{"isValid": true, "isSuspicious": false, "analysis": "Infração válida", "confidence": 0.97}""";

        var result = AiInfringementValidator.ParseResponse(json);

        result.IsValid.Should().BeTrue();
        result.IsSuspicious.Should().BeFalse();
        result.Analysis.Should().Be("Infração válida");
        result.Confidence.Should().Be(0.97);
    }

    [Fact]
    public void ParseResponse_WhenAiFlagsSuspicious_ShouldReturnIsSuspiciousTrue()
    {
        var json = """{"isValid": true, "isSuspicious": true, "analysis": "Placa fora do padrão Mercosul/antigo", "confidence": 0.88}""";

        var result = AiInfringementValidator.ParseResponse(json);

        result.IsValid.Should().BeTrue();
        result.IsSuspicious.Should().BeTrue();
        result.Analysis.Should().Be("Placa fora do padrão Mercosul/antigo");
    }

    [Fact]
    public void ParseResponse_WhenAiRejectsInfringement_ShouldReturnIsValidFalse()
    {
        var json = """{"isValid": false, "isSuspicious": false, "analysis": "Código CTB 100 fora da faixa válida", "confidence": 0.99}""";

        var result = AiInfringementValidator.ParseResponse(json);

        result.IsValid.Should().BeFalse();
        result.IsSuspicious.Should().BeFalse();
    }

    [Fact]
    public void ParseResponse_WhenJsonWrappedInMarkdown_ShouldStillParseCorrectly()
    {
        var content = "```json\n{\"isValid\": true, \"isSuspicious\": false, \"analysis\": \"OK\", \"confidence\": 0.9}\n```";

        var result = AiInfringementValidator.ParseResponse(content);

        result.IsValid.Should().BeTrue();
        result.Analysis.Should().Be("OK");
    }

    [Fact]
    public void ParseResponse_WhenContentIsEmpty_ShouldReturnFallbackValid()
    {
        var result = AiInfringementValidator.ParseResponse(string.Empty);

        result.IsValid.Should().BeTrue();
        result.IsSuspicious.Should().BeFalse();
        result.Confidence.Should().Be(0.5);
    }

    [Fact]
    public void ParseResponse_WhenJsonIsMalformed_ShouldReturnFallbackValid()
    {
        var result = AiInfringementValidator.ParseResponse("{ this is not json }");

        result.IsValid.Should().BeTrue();
        result.Confidence.Should().Be(0.5);
    }

    // --- BuildPrompt tests ---

    [Fact]
    public void BuildPrompt_ShouldContainAllInputFields()
    {
        var prompt = AiInfringementValidator.BuildPrompt("ABC-1234", 7455, 293.47m, "Fornecedor_A");

        prompt.Should().Contain("ABC-1234");
        prompt.Should().Contain("7455");
        prompt.Should().Contain("293,47").Or.Contain("293.47");
        prompt.Should().Contain("Fornecedor_A");
    }

    [Fact]
    public void BuildPrompt_ShouldIncludeCTBValidationCriteria()
    {
        var prompt = AiInfringementValidator.BuildPrompt("ABC-1234", 7455, 100m, "Fornecedor_A");

        prompt.Should().Contain("500");
        prompt.Should().Contain("999");
        prompt.Should().Contain("Mercosul");
    }
}
