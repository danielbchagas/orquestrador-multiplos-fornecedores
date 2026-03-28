namespace Supplier.Ingestion.Orchestrator.Api.Security;

public static class PlateObfuscator
{
    /// <summary>Retorna "ABC-***" ou "ABC9***" para logs — nunca a placa completa.</summary>
    public static string Mask(string plate)
    {
        if (string.IsNullOrWhiteSpace(plate) || plate.Length < 4)
            return "***";
        return plate[..3] + "***";
    }
}
