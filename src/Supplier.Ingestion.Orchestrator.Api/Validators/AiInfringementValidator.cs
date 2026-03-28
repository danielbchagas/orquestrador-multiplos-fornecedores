using Anthropic;
using Anthropic.Models.Messages;
using Supplier.Ingestion.Orchestrator.Api.Security;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Supplier.Ingestion.Orchestrator.Api.Validators;

public class AiInfringementValidator : IAiInfringementValidator
{
    private readonly AnthropicClient _client;
    private readonly ILogger<AiInfringementValidator> _logger;
    private readonly string _model;
    private readonly int _maxTokens;
    private readonly string _systemPrompt;

    public AiInfringementValidator(AnthropicClient client, ILogger<AiInfringementValidator> logger, IConfiguration configuration)
    {
        _client = client;
        _logger = logger;
        _model = configuration["Anthropic:Model"] ?? "claude-opus-4-6";
        _maxTokens = int.TryParse(configuration["Anthropic:MaxTokens"], out var tokens) ? tokens : 512;
        _systemPrompt = configuration["Anthropic:SystemPrompt"]
            ?? "Você é um especialista em infrações de trânsito brasileiras. Analise os dados da infração e retorne APENAS um JSON válido sem markdown, sem explicação adicional.";
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
                Model = _model,
                MaxTokens = _maxTokens,
                System = _systemPrompt,
                Messages =
                [
                    new() { Role = Role.User, Content = BuildPrompt(plate, infringementCode, amount, originSystem) }
                ]
            };

            _logger.LogInformation("Chamando IA para validação de infração. Placa: {Plate}, Código: {Code}",
                PlateObfuscator.Mask(plate), infringementCode);

            var response = await _client.Messages.Create(parameters, cancellationToken);
            var content = ExtractTextContent(response);

            _logger.LogInformation("Resposta da IA recebida. Tamanho: {Length}", content.Length);

            return ParseResponse(content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Validação IA falhou. Placa: {Plate}", PlateObfuscator.Mask(plate));
            return new AiValidationResult(
                IsValid: false,
                IsSuspicious: true,
                Analysis: "Validação IA indisponível — infração retida para revisão manual.",
                Confidence: 0.0);
        }
    }

    private static string SanitizeForPrompt(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var cleaned = Regex.Replace(input, @"[\r\n\t]", " ");
        cleaned = Regex.Replace(cleaned, @"(ignore|esquece?a?|desconsidere?|system\s*prompt|</?[a-z]+>)", " ", RegexOptions.IgnoreCase);

        return cleaned.Length > 50 ? cleaned[..50] : cleaned;
    }

    public static string BuildPrompt(string plate, int infringementCode, decimal amount, string originSystem) =>
        $$"""
        Analise esta infração de trânsito brasileira:
        - Placa: {{SanitizeForPrompt(plate)}}
        - Código CTB: {{infringementCode}}
        - Valor: R$ {{amount:F2}}
        - Sistema de origem: {{SanitizeForPrompt(originSystem)}}

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
