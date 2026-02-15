namespace Rag.Core.Abstractions;

/// <summary>
/// Generic cache service interface for distributed caching.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Get value from cache.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Set value in cache with expiration.
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Remove value from cache.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if key exists in cache.
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear all cache entries (use with caution).
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cache statistics.
    /// </summary>
    Task<CacheStats> GetStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Cache statistics.
/// </summary>
public class CacheStats
{
    public long TotalKeys { get; set; }
    public long MemoryUsageBytes { get; set; }
    public long HitCount { get; set; }
    public long MissCount { get; set; }
    public double HitRate => HitCount + MissCount > 0 ? (double)HitCount / (HitCount + MissCount) : 0;
}
