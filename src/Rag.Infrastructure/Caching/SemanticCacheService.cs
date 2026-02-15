using Microsoft.Extensions.Logging;
using Rag.Core.Abstractions;
using Rag.Core.Models;
using System.Security.Cryptography;
using System.Text;

namespace Rag.Infrastructure.Caching;

/// <summary>
/// Semantic cache that uses embeddings to find similar queries.
/// </summary>
public class SemanticCacheService : ISemanticCache
{
    private readonly IEmbeddingModel _embeddings;
    private readonly ICacheService _cache;
    private readonly SemanticCacheSettings _settings;
    private readonly ILogger<SemanticCacheService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private long _cacheHits;
    private long _cacheMisses;

    public SemanticCacheService(
        IEmbeddingModel embeddings,
        ICacheService cache,
        SemanticCacheSettings settings,
        ILogger<SemanticCacheService> logger)
    {
        _embeddings = embeddings;
        _cache = cache;
        _settings = settings;
        _logger = logger;
    }

    public async Task<CachedQueryResult?> GetSimilarAsync(string query, string tenantId, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return null;
        }

        try
        {
            // Get embedding for the query
            var embeddingResult = await _embeddings.EmbedAsync(query, cancellationToken);
            var queryEmbedding = embeddingResult.Embedding;

            // Get all cached queries for this tenant
            var cacheKey = GetCacheListKey(tenantId);
            var cachedQueries = await _cache.GetAsync<List<CachedQuery>>(cacheKey, cancellationToken);

            if (cachedQueries == null || cachedQueries.Count == 0)
            {
                Interlocked.Increment(ref _cacheMisses);
                _logger.LogDebug("Semantic cache MISS: No cached queries for tenant {TenantId}", tenantId);
                return null;
            }

            // Find most similar cached query
            var bestMatch = FindMostSimilar(queryEmbedding, cachedQueries);

            if (bestMatch == null || bestMatch.Value.Similarity < _settings.SimilarityThreshold)
            {
                Interlocked.Increment(ref _cacheMisses);
                _logger.LogDebug("Semantic cache MISS: Best similarity {Similarity} below threshold {Threshold}",
                    bestMatch?.Similarity ?? 0, _settings.SimilarityThreshold);
                return null;
            }

            Interlocked.Increment(ref _cacheHits);
            _logger.LogInformation("Semantic cache HIT: Query='{Query}' matched '{CachedQuery}' with similarity {Similarity:F3}",
                query, bestMatch.Value.Query.Query, bestMatch.Value.Similarity);

            // Update access statistics
            bestMatch.Value.Query.AccessCount++;
            bestMatch.Value.Query.LastAccessedAt = DateTime.UtcNow;
            await UpdateCachedQuery(tenantId, bestMatch.Value.Query, cancellationToken);

            return new CachedQueryResult
            {
                Query = bestMatch.Value.Query.Query,
                Response = bestMatch.Value.Query.Response,
                Citations = bestMatch.Value.Query.Citations,
                SimilarityScore = bestMatch.Value.Similarity,
                CachedAt = bestMatch.Value.Query.CachedAt,
                TokenUsage = bestMatch.Value.Query.TokenUsage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting similar query from semantic cache");
            Interlocked.Increment(ref _cacheMisses);
            return null;
        }
    }

    public async Task StoreAsync(string query, string response, List<Citation> citations, string tenantId, TokenUsage tokenUsage, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return;
        }

        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            // Get embedding for the query
            var embeddingResult = await _embeddings.EmbedAsync(query, cancellationToken);

            var cachedQuery = new CachedQuery
            {
                Query = query,
                QueryEmbedding = embeddingResult.Embedding,
                Response = response,
                Citations = citations,
                TenantId = tenantId,
                CachedAt = DateTime.UtcNow,
                TokenUsage = tokenUsage,
                AccessCount = 0,
                LastAccessedAt = DateTime.UtcNow
            };

            // Get existing cached queries
            var cacheKey = GetCacheListKey(tenantId);
            var cachedQueries = await _cache.GetAsync<List<CachedQuery>>(cacheKey, cancellationToken) ?? new List<CachedQuery>();

            // Add new query
            cachedQueries.Add(cachedQuery);

            // Prune if exceeds max size (keep most recently accessed)
            if (cachedQueries.Count > _settings.MaxCacheSize)
            {
                cachedQueries = cachedQueries
                    .OrderByDescending(q => q.LastAccessedAt)
                    .Take(_settings.MaxCacheSize)
                    .ToList();
            }

            // Store back in cache
            var ttl = TimeSpan.FromMinutes(_settings.DefaultTTLMinutes);
            await _cache.SetAsync(cacheKey, cachedQueries, ttl, cancellationToken);

            _logger.LogDebug("Stored query in semantic cache for tenant {TenantId}. Total cached: {Count}",
                tenantId, cachedQueries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing query in semantic cache");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ClearAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                await _cache.ClearAsync(cancellationToken);
                _logger.LogWarning("Cleared entire semantic cache");
            }
            else
            {
                var cacheKey = GetCacheListKey(tenantId);
                await _cache.RemoveAsync(cacheKey, cancellationToken);
                _logger.LogInformation("Cleared semantic cache for tenant {TenantId}", tenantId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing semantic cache");
        }
    }

    public async Task<SemanticCacheStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheStats = await _cache.GetStatsAsync(cancellationToken);

            return new SemanticCacheStats
            {
                TotalCachedQueries = cacheStats.TotalKeys,
                CacheHits = _cacheHits,
                CacheMisses = _cacheMisses,
                AverageSimilarityScore = 0 // TODO: Track this
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting semantic cache stats");
            return new SemanticCacheStats
            {
                CacheHits = _cacheHits,
                CacheMisses = _cacheMisses
            };
        }
    }

    private (CachedQuery Query, double Similarity)? FindMostSimilar(float[] queryEmbedding, List<CachedQuery> cachedQueries)
    {
        double bestSimilarity = 0;
        CachedQuery? bestMatch = null;

        foreach (var cached in cachedQueries)
        {
            var similarity = CosineSimilarity(queryEmbedding, cached.QueryEmbedding);
            if (similarity > bestSimilarity)
            {
                bestSimilarity = similarity;
                bestMatch = cached;
            }
        }

        return bestMatch != null ? (bestMatch, bestSimilarity) : null;
    }

    private double CosineSimilarity(float[] vector1, float[] vector2)
    {
        if (vector1.Length != vector2.Length)
        {
            return 0;
        }

        double dotProduct = 0;
        double magnitude1 = 0;
        double magnitude2 = 0;

        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
            magnitude1 += vector1[i] * vector1[i];
            magnitude2 += vector2[i] * vector2[i];
        }

        magnitude1 = Math.Sqrt(magnitude1);
        magnitude2 = Math.Sqrt(magnitude2);

        if (magnitude1 == 0 || magnitude2 == 0)
        {
            return 0;
        }

        return dotProduct / (magnitude1 * magnitude2);
    }

    private async Task UpdateCachedQuery(string tenantId, CachedQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var cacheKey = GetCacheListKey(tenantId);
            var cachedQueries = await _cache.GetAsync<List<CachedQuery>>(cacheKey, cancellationToken);

            if (cachedQueries != null)
            {
                var existing = cachedQueries.FirstOrDefault(q => q.Id == query.Id);
                if (existing != null)
                {
                    existing.AccessCount = query.AccessCount;
                    existing.LastAccessedAt = query.LastAccessedAt;
                    
                    var ttl = TimeSpan.FromMinutes(_settings.DefaultTTLMinutes);
                    await _cache.SetAsync(cacheKey, cachedQueries, ttl, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update cached query statistics");
        }
    }

    private string GetCacheListKey(string tenantId)
    {
        return $"semantic:queries:{tenantId}";
    }
}
