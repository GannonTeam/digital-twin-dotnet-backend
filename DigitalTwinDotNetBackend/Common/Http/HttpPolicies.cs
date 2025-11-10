using System.Net;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace Common.Http;

public static class HttpPolicies
{
    public static ResiliencePipeline<HttpResponseMessage> CreateResiliencePolicy(int maxRetries, ILogger logger)
    {
        maxRetries = Math.Max(1, maxRetries);
        
        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();

        builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
        {
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .HandleResult(r =>
                {
                    var code = (int)r.StatusCode;
                    return code is 408 or 429 || (code >= 500 && code != 501 && code != 505);
                }),

            MaxRetryAttempts = maxRetries,
            DelayGenerator = args =>
            {
                var baseDelayMs = 350 * Math.Pow(2, args.AttemptNumber - 1);
                var jitter = Random.Shared.NextDouble() * 250;
                var delay = TimeSpan.FromMilliseconds(baseDelayMs + jitter);
                return new ValueTask<TimeSpan?>(delay);
            },

            OnRetry = args =>
            {
                var baseDelayMs = 350 * Math.Pow(2, args.AttemptNumber - 1);
                var jitter = Random.Shared.NextDouble() * 250;
                var effectiveDelay = TimeSpan.FromMilliseconds(baseDelayMs + jitter);

                var resp = args.Outcome.Result;
                string? remain = null;

                if (resp != null && resp.Headers.TryGetValues("Retry-After", out var values))
                {
                    var retryAfter = values.FirstOrDefault();
                    if (int.TryParse(retryAfter, out var secs))
                        effectiveDelay = TimeSpan.FromSeconds(Math.Clamp(secs, 1, 30));
                    else if (DateTimeOffset.TryParse(retryAfter, out var when))
                    {
                        var candidate = when - DateTimeOffset.UtcNow;
                        if (candidate > TimeSpan.Zero && candidate < TimeSpan.FromSeconds(60))
                            effectiveDelay = candidate;
                        }
                }

                if (resp?.Headers.TryGetValues("X-RateLimit-Remaining", out var rem) == true)
                    remain = rem.FirstOrDefault();

                if (resp != null)
                {
                    logger.LogWarning(
                        "Retry {Attempt} after {Delay} due to HTTP {Status}. X-RateLimit-Remaining={Remain}",
                        args.AttemptNumber, effectiveDelay, (int)resp.StatusCode, remain);
                }
                else
                {
                    logger.LogWarning(
                        "Retry {Attempt} after {Delay} due to {ErrorType}",
                        args.AttemptNumber, effectiveDelay,
                        args.Outcome.Exception?.GetType().Name ?? "error");
                }
                return new ValueTask(Task.Delay(effectiveDelay, args.Context.CancellationToken));
            }
        });

        return builder.Build();
    }
}