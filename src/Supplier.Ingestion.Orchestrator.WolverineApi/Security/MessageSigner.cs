using System.Security.Cryptography;
using System.Text;

namespace Supplier.Ingestion.Orchestrator.WolverineApi.Security;

public static class MessageSigner
{
    public static string Sign(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        return Convert.ToBase64String(hmac.ComputeHash(payloadBytes));
    }

    public static bool Verify(string payload, string signature, string secret)
    {
        var expected = Encoding.UTF8.GetBytes(Sign(payload, secret));
        var provided = Encoding.UTF8.GetBytes(signature ?? string.Empty);
        return CryptographicOperations.FixedTimeEquals(expected, provided);
    }
}
