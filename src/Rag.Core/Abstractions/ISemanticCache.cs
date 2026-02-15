using Rag.Core.Models;

namespace Rag.Core.Abstractions;

/// <summary>
/// Semantic cache that finds similar queries using vector similarity.
/// </summary>
public interface ISemanticCache
{
    /// <summary>
    /// Get cached response for a semantically similar query.
    /// </summary>
    Task<CachedQueryResult?> GetSimilarAsync(string query, string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Store query and response in semantic cache.
    /// </summary>
    Task StoreAsync(string query, string response, List<Citation> citations, string tenantId, TokenUsage tokenUsage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear semantic cache for a tenant.
    /// </summary>
    Task ClearAsync(string? tenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get semantic cache statistics.
    /// </summary>
    Task<SemanticCacheStats> GetStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Cached query result with similarity score.
/// </summary>
public class CachedQueryResult
{
    public string Query { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public List<Citation> Citations { get; set; } = new();
    public double SimilarityScore { get; set; }
    public DateTime CachedAt { get; set; }
    public TokenUsage TokenUsage { get; set; } = new();
}

/// <summary>
/// Semantic cache statistics.
/// </summary>
public class SemanticCacheStats
{
    public long TotalCachedQueries { get; set; }
    public long CacheHits { get; set; }
    public long CacheMisses { get; set; }
    public double AverageSimilarityScore { get; set; }
    public double HitRate => CacheHits + CacheMisses > 0 ? (double)CacheHits / (CacheHits + CacheMisses) : 0;
}
