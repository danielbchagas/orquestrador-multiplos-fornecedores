using Anthropic;
using Anthropic.Models.Messages;
using System.Text.Json;

namespace Supplier.Ingestion.Orchestrator.Api.Validators;

public class AiInfringementValidator : IAiInfringementValidator
{
    private readonly AnthropicClient _client;
    private readonly ILogger<AiInfringementValidator> _logger;

    private const string SystemPrompt =
        "Você é um especialista em infrações de trânsito brasileiras. " +
        "Analise os dados da infração e retorne APENAS um JSON válido sem markdown, sem explicação adicional.";

    public AiInfringementValidator(AnthropicClient client, ILogger<AiInfringementValidator> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<AiValidationResult> ValidateAsync(
        string plate,
        int infringementCode,
        decimal amount,
        string originSystem,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new MessageCreateParams
            {
                Model = "claude-opus-4-6",
                MaxTokens = 512,
                System = SystemPrompt,
                Messages =
                [
                    new() { Role = Role.User, Content = BuildPrompt(plate, infringementCode, amount, originSystem) }
                ]
            };

            _logger.LogInformation("Calling Claude AI for infringement validation. Plate: {Plate}, Code: {Code}",
                plate, infringementCode);

            var response = await _client.Messages.Create(parameters, cancellationToken);
            var content = ExtractTextContent(response);

            _logger.LogInformation("AI response received. Raw content length: {Length}", content.Length);

            return ParseResponse(content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI validation failed, defaulting to valid. Plate: {Plate}", plate);
            return new AiValidationResult(true, false, "AI validation unavailable", 0.5);
        }
    }

    public static string BuildPrompt(string plate, int infringementCode, decimal amount, string originSystem) =>
        $$"""
        Analise esta infração de trânsito brasileira:
        - Placa: {{plate}}
        - Código CTB: {{infringementCode}}
        - Valor: R$ {{amount:F2}}
        - Sistema de origem: {{originSystem}}

        Verifique:
        1. Se a placa está no formato válido (padrão antigo AAA-9999 ou Mercosul AAA9A99)
        2. Se o código CTB é plausível (faixa 500-999)
        3. Se o valor é compatível com o tipo de infração:
           - Leves: R$88 a R$195
           - Médias: R$130 a R$293
           - Graves: R$195 a R$880
           - Gravíssimas: R$293 ou mais
        4. Se há inconsistências suspeitas entre os campos

        Retorne APENAS este JSON (sem markdown, sem texto extra):
        {"isValid": true, "isSuspicious": false, "analysis": "explicação concisa em português", "confidence": 0.95}
        """;

    internal static string ExtractTextContent(Message response)
    {
        return response.Content
            .Select(block => block.Value)
            .OfType<TextBlock>()
            .Select(tb => tb.Text)
            .FirstOrDefault() ?? string.Empty;
    }

    public static AiValidationResult ParseResponse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new AiValidationResult(true, false, "Empty AI response", 0.5);

        try
        {
            var start = content.IndexOf('{');
            var end = content.LastIndexOf('}');

            if (start >= 0 && end > start)
            {
                var json = content[start..(end + 1)];
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                return new AiValidationResult(
                    IsValid: root.GetProperty("isValid").GetBoolean(),
                    IsSuspicious: root.GetProperty("isSuspicious").GetBoolean(),
                    Analysis: root.GetProperty("analysis").GetString() ?? string.Empty,
                    Confidence: root.GetProperty("confidence").GetDouble());
            }
        }
        catch (Exception)
        {
            // ignored — fallback below
        }

        return new AiValidationResult(true, false, $"Could not parse AI response: {content[..Math.Min(content.Length, 100)]}", 0.5);
    }
}
