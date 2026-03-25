using System.Security.Cryptography;
using System.Text;

namespace Supplier.B.Producer.Api.Security;

public static class MessageSigner
{
    public static string Sign(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        return Convert.ToBase64String(hmac.ComputeHash(payloadBytes));
    }
}
