using FluentAssertions;
using Supplier.Ingestion.Orchestrator.Api.Validators;

namespace Supplier.Ingestion.Orchestrator.Tests.UnitTests.Validators;

public class InfringementValidatorTests
{
    // --- Hard validations ---

    [Fact]
    public void GivenNullPlate_WhenValidate_ThenIsInvalidWithZeroScore()
    {
        var result = InfringementValidator.Validate(null!, 100m, "EXT-001");

        result.IsValid.Should().BeFalse();
        result.ConfidenceScore.Should().Be(0f);
        result.Errors.Should().Contain("Placa obrigatória");
    }

    [Fact]
    public void GivenNegativeAmount_WhenValidate_ThenIsInvalidWithReducedScore()
    {
        var result = InfringementValidator.Validate("ABC-1234", -50m, "EXT-001");

        result.IsValid.Should().BeFalse();
        result.ConfidenceScore.Should().BeLessThan(1f);
        result.Errors.Should().Contain("Valor inválido");
    }

    [Fact]
    public void GivenNullExternalId_WhenValidate_ThenIsInvalidWithZeroScore()
    {
        var result = InfringementValidator.Validate("ABC-1234", 100m, null!);

        result.IsValid.Should().BeFalse();
        result.ConfidenceScore.Should().Be(0f);
        result.Errors.Should().Contain("ID de origem não informado");
    }

    [Fact]
    public void GivenMultipleHardErrors_WhenValidate_ThenErrorsAreJoined()
    {
        var result = InfringementValidator.Validate(null!, -10m, null!);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Placa obrigatória");
        result.Errors.Should().Contain("ID de origem não informado");
    }

    // --- Valid with full confidence ---

    [Theory]
    [InlineData("ABC-1234")]   // Old format with dash
    [InlineData("ABC1234")]    // Old format without dash
    [InlineData("ABC1D23")]    // Mercosul format
    public void GivenValidBrazilianPlate_WhenValidate_ThenNoPlateRiskFlag(string plate)
    {
        var result = InfringementValidator.Validate(plate, 100m, "EXT-001");

        result.IsValid.Should().BeTrue();
        result.RiskFlags.Should().NotContain("INVALID_PLATE_FORMAT");
        result.ConfidenceScore.Should().Be(1.0f);
    }

    // --- Soft validations ---

    [Fact]
    public void GivenInvalidPlateFormat_WhenValidate_ThenIsValidButScoreReducedAndFlagAdded()
    {
        var result = InfringementValidator.Validate("INVALID-PLATE-999", 100m, "EXT-001");

        result.IsValid.Should().BeTrue();
        result.RiskFlags.Should().Contain("INVALID_PLATE_FORMAT");
        result.ConfidenceScore.Should().BeApproximately(0.8f, 0.001f);
    }

    [Fact]
    public void GivenZeroAmount_WhenValidate_ThenIsValidButScoreReducedAndFlagAdded()
    {
        var result = InfringementValidator.Validate("ABC-1234", 0m, "EXT-001");

        result.IsValid.Should().BeTrue();
        result.RiskFlags.Should().Contain("AMOUNT_ZERO");
        result.ConfidenceScore.Should().BeApproximately(0.9f, 0.001f);
    }

    [Fact]
    public void GivenHighAmount_WhenValidate_ThenIsValidButScoreReducedAndFlagAdded()
    {
        var result = InfringementValidator.Validate("ABC-1234", 15_000m, "EXT-001");

        result.IsValid.Should().BeTrue();
        result.RiskFlags.Should().Contain("HIGH_AMOUNT");
        result.ConfidenceScore.Should().BeApproximately(0.9f, 0.001f);
    }

    [Fact]
    public void GivenInvalidPlateAndHighAmount_WhenValidate_ThenScoreCombinesBothDeductions()
    {
        var result = InfringementValidator.Validate("INVALID", 20_000m, "EXT-001");

        result.IsValid.Should().BeTrue();
        result.RiskFlags.Should().Contain("INVALID_PLATE_FORMAT");
        result.RiskFlags.Should().Contain("HIGH_AMOUNT");
        result.ConfidenceScore.Should().BeApproximately(0.7f, 0.001f);
    }

    [Fact]
    public void GivenFullyValidData_WhenValidate_ThenScoreIsOneAndNoRiskFlags()
    {
        var result = InfringementValidator.Validate("ABC-1234", 250m, "EXT-001");

        result.IsValid.Should().BeTrue();
        result.ConfidenceScore.Should().Be(1.0f);
        result.RiskFlags.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
    }
}
