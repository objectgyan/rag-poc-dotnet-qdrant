using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Rag.Core.Models;
using System.Diagnostics;

namespace Rag.Api.HealthChecks;

/// <summary>
/// PHASE 7: Health check for Qdrant connectivity and availability.
/// </summary>
public sealed class QdrantHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly QdrantSettings _settings;
    private readonly ILogger<QdrantHealthCheck> _logger;

    public QdrantHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptions<QdrantSettings> settings,
        ILogger<QdrantHealthCheck> logger)
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
            var httpClient = _httpClientFactory.CreateClient("QdrantHttpClient");

            // Try to list collections to verify connectivity
            var url = $"{_settings.Url.TrimEnd('/')}/collections";
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

            var response = await httpClient.GetAsync(url, linkedCts.Token);

            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                var data = new Dictionary<string, object>
                {
                    ["url"] = _settings.Url,
                    ["responseTime"] = $"{stopwatch.ElapsedMilliseconds}ms",
                    ["timestamp"] = DateTimeOffset.UtcNow
                };

                _logger.LogDebug("Qdrant health check succeeded in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

                return HealthCheckResult.Healthy(
                    description: $"Qdrant is healthy (response time: {stopwatch.ElapsedMilliseconds}ms)",
                    data: data);
            }
            else
            {
                var data = new Dictionary<string, object>
                {
                    ["url"] = _settings.Url,
                    ["responseTime"] = $"{stopwatch.ElapsedMilliseconds}ms",
                    ["statusCode"] = (int)response.StatusCode,
                    ["timestamp"] = DateTimeOffset.UtcNow
                };

                _logger.LogWarning("Qdrant health check returned status code {StatusCode} after {ElapsedMs}ms", 
                    response.StatusCode, stopwatch.ElapsedMilliseconds);

                return HealthCheckResult.Degraded(
                    description: $"Qdrant returned status code {response.StatusCode}",
                    data: data);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "Qdrant health check failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

            var data = new Dictionary<string, object>
            {
                ["url"] = _settings.Url,
                ["responseTime"] = $"{stopwatch.ElapsedMilliseconds}ms",
                ["error"] = ex.Message,
                ["timestamp"] = DateTimeOffset.UtcNow
            };

            return HealthCheckResult.Unhealthy(
                description: $"Qdrant is unhealthy: {ex.Message}",
                exception: ex,
                data: data);
        }
    }
}
