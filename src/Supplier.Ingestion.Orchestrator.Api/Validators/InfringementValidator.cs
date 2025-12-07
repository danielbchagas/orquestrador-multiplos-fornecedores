namespace Supplier.Ingestion.Orchestrator.Api.Validators;

public class InfringementValidator
{
    public static (bool IsValid, string Errors) Validate(string plate, decimal amount, string externalId)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(plate))
            errors.Add("Placa obrigatória");

        if (amount < 0)
            errors.Add($"Valor inválido: {amount}");

        if (string.IsNullOrWhiteSpace(externalId))
            errors.Add("ID de origem não informado");

        return errors.Any()
            ? (false, string.Join(" | ", errors))
            : (true, string.Empty);
    }
}
