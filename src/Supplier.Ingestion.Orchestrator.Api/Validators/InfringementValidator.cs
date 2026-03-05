using System.Text.RegularExpressions;

namespace Supplier.Ingestion.Orchestrator.Api.Validators;

public class InfringementValidator
{
    // Brazilian plate formats: old (ABC-1234) and Mercosul (ABC1D23)
    private static readonly Regex OldPlatePattern = new(@"^[A-Z]{3}-?[0-9]{4}$", RegexOptions.Compiled);
    private static readonly Regex MercosulPlatePattern = new(@"^[A-Z]{3}[0-9][A-Z][0-9]{2}$", RegexOptions.Compiled);

    private const decimal HighAmountThreshold = 10_000m;

    public static ValidationResult Validate(string plate, decimal amount, string externalId)
    {
        var errors = new List<string>();
        var riskFlags = new List<string>();
        float score = 1.0f;

        if (string.IsNullOrWhiteSpace(plate))
        {
            errors.Add("Placa obrigatória");
            score = 0f;
        }

        if (amount < 0)
        {
            errors.Add($"Valor inválido: {amount}");
            score = Math.Max(0f, score - 0.5f);
        }

        if (string.IsNullOrWhiteSpace(externalId))
        {
            errors.Add("ID de origem não informado");
            score = 0f;
        }

        if (errors.Count > 0)
            return new ValidationResult(false, string.Join(" | ", errors), score, riskFlags.ToArray());

        // Soft validations: reduce score and add risk flags
        var normalizedPlate = plate.Trim().ToUpperInvariant();
        if (!OldPlatePattern.IsMatch(normalizedPlate) && !MercosulPlatePattern.IsMatch(normalizedPlate))
        {
            riskFlags.Add("INVALID_PLATE_FORMAT");
            score -= 0.2f;
        }

        if (amount == 0m)
        {
            riskFlags.Add("AMOUNT_ZERO");
            score -= 0.1f;
        }
        else if (amount > HighAmountThreshold)
        {
            riskFlags.Add("HIGH_AMOUNT");
            score -= 0.1f;
        }

        score = Math.Max(0f, score);

        return new ValidationResult(true, string.Empty, score, riskFlags.ToArray());
    }
}
