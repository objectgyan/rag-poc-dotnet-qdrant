using Microsoft.Extensions.Logging;
using Rag.Core.Abstractions;
using Rag.Core.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace Rag.Infrastructure.Caching;

/// <summary>
/// Redis-based distributed cache implementation.
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly RedisSettings _settings;
    private readonly ILogger<RedisCacheService> _logger;
    private long _hitCount;
    private long _missCount;

    public RedisCacheService(
        IConnectionMultiplexer redis,
        RedisSettings settings,
        ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _db = redis.GetDatabase();
        _settings = settings;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var fullKey = GetFullKey(key);
            var value = await _db.StringGetAsync(fullKey);

            if (value.IsNullOrEmpty)
            {
                Interlocked.Increment(ref _missCount);
                _logger.LogDebug("Cache MISS: {Key}", key);
                return null;
            }

            Interlocked.Increment(ref _hitCount);
            _logger.LogDebug("Cache HIT: {Key}", key);

            var result = JsonSerializer.Deserialize<T>((string)value!);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting value from cache for key: {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var fullKey = GetFullKey(key);
            var json = JsonSerializer.Serialize(value);
            var ttl = expiration ?? TimeSpan.FromMinutes(_settings.DefaultExpirationMinutes);

            await _db.StringSetAsync(fullKey, json, ttl);
            _logger.LogDebug("Cache SET: {Key} (TTL: {TTL})", key, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting value in cache for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullKey = GetFullKey(key);
            await _db.KeyDeleteAsync(fullKey);
            _logger.LogDebug("Cache REMOVE: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing value from cache for key: {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullKey = GetFullKey(key);
            return await _db.KeyExistsAsync(fullKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence in cache for key: {Key}", key);
            return false;
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoints = _redis.GetEndPoints();
            foreach (var endpoint in endpoints)
            {
                var server = _redis.GetServer(endpoint);
                await server.FlushDatabaseAsync();
            }
            _logger.LogWarning("Cache CLEARED");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
        }
    }

    public async Task<CacheStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoints = _redis.GetEndPoints();
            var server = _redis.GetServer(endpoints.First());
            var info = await server.InfoAsync("memory");
            
            var memoryInfo = info.FirstOrDefault();
            var memoryUsed = memoryInfo?.FirstOrDefault(x => x.Key == "used_memory").Value ?? "0";
            var keyCount = await _db.ExecuteAsync("DBSIZE");

            return new CacheStats
            {
                TotalKeys = (long)keyCount,
                MemoryUsageBytes = long.Parse(memoryUsed),
                HitCount = _hitCount,
                MissCount = _missCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache stats");
            return new CacheStats
            {
                HitCount = _hitCount,
                MissCount = _missCount
            };
        }
    }

    private string GetFullKey(string key)
    {
        return $"{_settings.InstanceName}{key}";
    }
}
