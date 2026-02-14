using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Rag.Core.Models;
using System.Diagnostics;

namespace Rag.Api.HealthChecks;

/// <summary>
/// PHASE 7: Health check for Claude API connectivity and availability.
/// </summary>
public sealed class ClaudeHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AnthropicSettings _settings;
    private readonly ILogger<ClaudeHealthCheck> _logger;

    public ClaudeHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptions<AnthropicSettings> settings,
        ILogger<ClaudeHealthCheck> logger)
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
            var httpClient = _httpClientFactory.CreateClient("AnthropicClient");

            // Simple HEAD request to check API availability
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/messages")
            {
                Headers =
                {
                    { "x-api-key", _settings.ApiKey },
                    { "anthropic-version", "2023-06-01" }
                }
            };

            // Set a short timeout for health check (5 seconds)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

            var response = await httpClient.SendAsync(request, linkedCts.Token);

            stopwatch.Stop();

            // 400 is expected since we're not sending a valid request body,
            // but it confirms the API is reachable
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
                response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Claude API health check passed in {Duration}ms", stopwatch.ElapsedMilliseconds);

                return HealthCheckResult.Healthy(
                    description: "Claude API is reachable",
                    data: new Dictionary<string, object>
                    {
                        ["ResponseTime"] = $"{stopwatch.ElapsedMilliseconds}ms",
                        ["Model"] = _settings.Model
                    });
            }

            _logger.LogWarning("Claude API returned unexpected status: {StatusCode}", response.StatusCode);

            return HealthCheckResult.Degraded(
                description: $"Claude API returned status {response.StatusCode}",
                data: new Dictionary<string, object>
                {
                    ["StatusCode"] = (int)response.StatusCode,
                    ["ResponseTime"] = $"{stopwatch.ElapsedMilliseconds}ms"
                });
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning("Claude API health check timed out after {Duration}ms", stopwatch.ElapsedMilliseconds);

            return HealthCheckResult.Unhealthy(
                description: "Claude API health check timed out",
                data: new Dictionary<string, object>
                {
                    ["Timeout"] = "5000ms",
                    ["Duration"] = $"{stopwatch.ElapsedMilliseconds}ms"
                });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Claude API health check failed");

            return HealthCheckResult.Unhealthy(
                description: "Claude API health check failed",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["Duration"] = $"{stopwatch.ElapsedMilliseconds}ms",
                    ["Error"] = ex.Message
                });
        }
    }
}
