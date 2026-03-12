using Polly;
using Polly.Retry;

namespace Supplier.Ingestion.Orchestrator.Api.Validators;

public class ResilientAiInfringementValidator : IAiInfringementValidator
{
    private readonly AiInfringementValidator _inner;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<ResilientAiInfringementValidator> _logger;

    public ResilientAiInfringementValidator(
        AiInfringementValidator inner,
        ILogger<ResilientAiInfringementValidator> logger)
    {
        _inner = inner;
        _logger = logger;

        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>(),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Retry #{AttemptNumber} para validação IA após erro. Aguardando {Delay}ms.",
                        args.AttemptNumber, args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(30))
            .Build();
    }

    public async Task<AiValidationResult> ValidateAsync(
        string plate,
        int infringementCode,
        decimal amount,
        string originSystem,
        CancellationToken cancellationToken = default)
    {
        return await _pipeline.ExecuteAsync(
            async ct => await _inner.ValidateAsync(plate, infringementCode, amount, originSystem, ct),
            cancellationToken);
    }
}
