using System.Text;
using Wolverine;

namespace Supplier.Ingestion.Orchestrator.WolverineApi.Security;

/// <summary>
/// Middleware Wolverine equivalente ao <c>KafkaSignatureVerificationFilter</c> da versão MassTransit:
/// valida o header <c>x-signature</c> (HMAC-SHA256 do corpo bruto) das mensagens dos fornecedores
/// e descarta silenciosamente as mensagens sem assinatura válida.
/// Aplicado apenas aos handlers de eventos de entrada (ver WolverineExtensions).
/// </summary>
public class KafkaSignatureVerificationMiddleware
{
    public static HandlerContinuation Before(
        Envelope envelope,
        IConfiguration configuration,
        ILogger<KafkaSignatureVerificationMiddleware> logger)
    {
        var signingKey = configuration["Kafka:SigningKey"] ?? string.Empty;

        if (string.IsNullOrEmpty(signingKey))
            return HandlerContinuation.Continue;

        if (!envelope.Headers.TryGetValue("x-signature", out var signature) || string.IsNullOrEmpty(signature))
        {
            logger.LogWarning("Mensagem sem header x-signature rejeitada. MessageId: {MessageId}", envelope.Id);
            return HandlerContinuation.Stop;
        }

        if (envelope.Data is not { Length: > 0 })
        {
            logger.LogWarning("Mensagem sem corpo bruto disponível para verificação. MessageId: {MessageId}", envelope.Id);
            return HandlerContinuation.Stop;
        }

        var payload = Encoding.UTF8.GetString(envelope.Data);

        if (!MessageSigner.Verify(payload, signature, signingKey))
        {
            logger.LogWarning("Assinatura HMAC inválida. Mensagem rejeitada. MessageId: {MessageId}", envelope.Id);
            return HandlerContinuation.Stop;
        }

        return HandlerContinuation.Continue;
    }
}
