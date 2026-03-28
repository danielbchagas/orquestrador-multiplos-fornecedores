using MassTransit;
using System.Text;

namespace Supplier.Ingestion.Orchestrator.Api.Security;

public class KafkaSignatureVerificationFilter<T>(IConfiguration configuration, ILogger<KafkaSignatureVerificationFilter<T>> logger)
    : IFilter<ConsumeContext<T>>
    where T : class
{
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        var signingKey = configuration["Kafka:SigningKey"] ?? string.Empty;

        if (!string.IsNullOrEmpty(signingKey))
        {
            if (!context.Headers.TryGetHeader("x-signature", out var rawSignature))
            {
                logger.LogWarning("Mensagem sem header x-signature rejeitada. MessageId: {MessageId}", context.MessageId);
                return;
            }

            var signature = rawSignature is byte[] bytes
                ? Encoding.UTF8.GetString(bytes)
                : rawSignature?.ToString() ?? string.Empty;

            var bodyBytes = context.ReceiveContext.Body.GetBytes();
            var payload = Encoding.UTF8.GetString(bodyBytes);

            if (!MessageSigner.Verify(payload, signature, signingKey))
            {
                logger.LogWarning("Assinatura HMAC inválida. Mensagem rejeitada. MessageId: {MessageId}", context.MessageId);
                return;
            }
        }

        await next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("kafkaSignatureVerification");
}
