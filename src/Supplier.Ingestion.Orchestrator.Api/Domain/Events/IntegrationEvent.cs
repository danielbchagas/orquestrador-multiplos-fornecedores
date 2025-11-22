using MassTransit;
using System.Security.Cryptography;
using System.Text;

namespace Supplier.Ingestion.Orchestrator.Api.Domain.Events;

public abstract record IntegrationEvent : CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; }

    protected IntegrationEvent(string businessKey)
    {
        if (string.IsNullOrWhiteSpace(businessKey))
            throw new ArgumentNullException(nameof(businessKey), "A chave de negócio é obrigatória para gerar o ID.");

        CorrelationId = GenerateDeterministicGuid(businessKey);
    }

    private static Guid GenerateDeterministicGuid(string input)
    {
        using var provider = MD5.Create();
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = provider.ComputeHash(inputBytes);
        return new Guid(hashBytes);
    }
}