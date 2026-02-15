using Microsoft.AspNetCore.Mvc;
using Rag.Core.Abstractions;
using Rag.Core.Services;

namespace Rag.Api.Controllers;

/// <summary>
/// Cache management and monitoring endpoints (Phase 9).
/// </summary>
[ApiController]
[Route("api/v1/cache")]
public class CacheController : ControllerBase
{
    private readonly ICacheService? _cache;
    private readonly ISemanticCache? _semanticCache;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CacheController> _logger;

    public CacheController(
        ITenantContext tenantContext,
        ILogger<CacheController> logger,
        ICacheService? cache = null,
        ISemanticCache? semanticCache = null)
    {
        _tenantContext = tenantContext;
        _logger = logger;
        _cache = cache;
        _semanticCache = semanticCache;
    }

    /// <summary>
    /// Get cache statistics.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        if (_cache == null && _semanticCache == null)
        {
            return Ok(new { enabled = false, message = "Caching is not enabled" });
        }

        var stats = new Dictionary<string, object>();

        if (_cache != null)
        {
            var cacheStats = await _cache.GetStatsAsync();
            stats["redis"] = new
            {
                total_keys = cacheStats.TotalKeys,
                memory_usage_bytes = cacheStats.MemoryUsageBytes,
                memory_usage_mb = cacheStats.MemoryUsageBytes / 1024.0 / 1024.0,
                hit_count = cacheStats.HitCount,
                miss_count = cacheStats.MissCount,
                hit_rate = cacheStats.HitRate
            };
        }

        if (_semanticCache != null)
        {
            var semanticStats = await _semanticCache.GetStatsAsync();
            stats["semantic"] = new
            {
                total_cached_queries = semanticStats.TotalCachedQueries,
                cache_hits = semanticStats.CacheHits,
                cache_misses = semanticStats.CacheMisses,
                hit_rate = semanticStats.HitRate,
                average_similarity = semanticStats.AverageSimilarityScore
            };
        }

        return Ok(new
        {
            enabled = true,
            stats,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Clear cache for current tenant or all tenants (admin).
    /// </summary>
    [HttpPost("clear")]
    public async Task<IActionResult> ClearCache([FromQuery] bool allTenants = false)
    {
        if (_cache == null && _semanticCache == null)
        {
            return BadRequest(new { error = "Caching is not enabled" });
        }

        var tenantId = allTenants ? null : _tenantContext.TenantId;

        if (_semanticCache != null)
        {
            await _semanticCache.ClearAsync(tenantId);
            _logger.LogWarning("Cleared semantic cache for tenant: {TenantId}", tenantId ?? "ALL");
        }

        if (_cache != null && allTenants)
        {
            await _cache.ClearAsync();
            _logger.LogWarning("Cleared entire Redis cache");
        }

        return Ok(new
        {
            message = allTenants ? "Cleared all cache" : $"Cleared cache for tenant {tenantId}",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Check if caching is enabled and healthy.
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> GetHealth()
    {
        var health = new Dictionary<string, object>
        {
            ["enabled"] = _cache != null || _semanticCache != null,
            ["redis_enabled"] = _cache != null,
            ["semantic_cache_enabled"] = _semanticCache != null
        };

        if (_cache != null)
        {
            try
            {
                var stats = await _cache.GetStatsAsync();
                health["redis_status"] = "healthy";
                health["redis_keys"] = stats.TotalKeys;
            }
            catch (Exception ex)
            {
                health["redis_status"] = "unhealthy";
                health["redis_error"] = ex.Message;
                _logger.LogError(ex, "Redis health check failed");
            }
        }

        return Ok(health);
    }

    /// <summary>
    /// Get cache configuration info (non-sensitive).
    /// </summary>
    [HttpGet("info")]
    public IActionResult GetInfo()
    {
        return Ok(new
        {
            redis_enabled = _cache != null,
            semantic_cache_enabled = _semanticCache != null,
            features = new[]
            {
                _cache != null ? "Redis Distributed Cache" : null,
                _semanticCache != null ? "Semantic Query Cache" : null
            }.Where(f => f != null).ToArray()
        });
    }
}
