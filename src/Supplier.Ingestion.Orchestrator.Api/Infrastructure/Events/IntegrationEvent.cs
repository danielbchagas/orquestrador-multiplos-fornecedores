using MassTransit;
using System.Security.Cryptography;
using System.Text;

namespace Supplier.Ingestion.Orchestrator.Api.Infrastructure.Events;

public abstract record IntegrationEvent : CorrelatedBy<Guid>
{
    // RFC 4122 DNS namespace — used as the UUID v5 namespace for business keys
    private static readonly Guid DnsNamespace = new("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

    public Guid CorrelationId { get; init; }

    protected IntegrationEvent(string businessKey)
    {
        if (string.IsNullOrWhiteSpace(businessKey))
            throw new ArgumentNullException(nameof(businessKey), "A chave de negócio é obrigatória para gerar o ID.");

        CorrelationId = GenerateDeterministicGuid(businessKey);
    }

    private static Guid GenerateDeterministicGuid(string input)
    {
        Span<byte> namespaceBytes = stackalloc byte[16];
        DnsNamespace.TryWriteBytes(namespaceBytes, bigEndian: true, out _);

        var nameBytes = Encoding.UTF8.GetBytes(input);
        var buffer = new byte[16 + nameBytes.Length];
        namespaceBytes.CopyTo(buffer);
        nameBytes.CopyTo(buffer, 16);

        var hash = SHA1.HashData(buffer);

        hash[6] = (byte)((hash[6] & 0x0F) | 0x50); // version 5
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80); // variant RFC 4122

        return new Guid(hash.AsSpan()[..16], bigEndian: true);
    }
}