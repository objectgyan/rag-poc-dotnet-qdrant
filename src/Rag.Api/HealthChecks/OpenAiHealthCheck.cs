using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Rag.Core.Models;
using System.Diagnostics;

namespace Rag.Api.HealthChecks;

/// <summary>
/// PHASE 7: Health check for OpenAI API connectivity and availability.
/// </summary>
public sealed class OpenAiHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OpenAiSettings _settings;
    private readonly ILogger<OpenAiHealthCheck> _logger;

    public OpenAiHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenAiSettings> settings,
        ILogger<OpenAiHealthCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var httpClient = _httpClientFactory.CreateClient("OpenAIClient");

            // Check models endpoint to verify API connectivity
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models")
            {
                Headers =
                {
                    { "Authorization", $"Bearer {_settings.ApiKey}" }
                }
            };

            // Set a short timeout for health check (5 seconds)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

            var response = await httpClient.SendAsync(request, linkedCts.Token);

            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("OpenAI API health check passed in {Duration}ms", stopwatch.ElapsedMilliseconds);

                return HealthCheckResult.Healthy(
                    description: "OpenAI API is reachable",
                    data: new Dictionary<string, object>
                    {
                        ["ResponseTime"] = $"{stopwatch.ElapsedMilliseconds}ms",
                        ["EmbeddingModel"] = _settings.EmbeddingModel
                    });
            }

            _logger.LogWarning("OpenAI API returned unexpected status: {StatusCode}", response.StatusCode);

            return HealthCheckResult.Degraded(
                description: $"OpenAI API returned status {response.StatusCode}",
                data: new Dictionary<string, object>
                {
                    ["StatusCode"] = (int)response.StatusCode,
                    ["ResponseTime"] = $"{stopwatch.ElapsedMilliseconds}ms"
                });
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning("OpenAI API health check timed out after {Duration}ms", stopwatch.ElapsedMilliseconds);

            return HealthCheckResult.Unhealthy(
                description: "OpenAI API health check timed out",
                data: new Dictionary<string, object>
                {
                    ["Timeout"] = "5000ms",
                    ["Duration"] = $"{stopwatch.ElapsedMilliseconds}ms"
                });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "OpenAI API health check failed");

            return HealthCheckResult.Unhealthy(
                description: "OpenAI API health check failed",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["Duration"] = $"{stopwatch.ElapsedMilliseconds}ms",
                    ["Error"] = ex.Message
                });
        }
    }
}
