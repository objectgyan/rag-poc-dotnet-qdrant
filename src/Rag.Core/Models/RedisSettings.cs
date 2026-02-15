namespace Rag.Core.Models;

/// <summary>
/// Redis configuration settings for distributed caching.
/// </summary>
public class RedisSettings
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public string InstanceName { get; set; } = "RagPoc:";
    public int DefaultExpirationMinutes { get; set; } = 60;
    public bool EnableCompression { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public int ConnectTimeout { get; set; } = 5000;
    public int ConnectRetry { get; set; } = 3;
}

/// <summary>
/// Semantic cache configuration.
/// </summary>
public class SemanticCacheSettings
{
    public bool Enabled { get; set; } = true;
    public double SimilarityThreshold { get; set; } = 0.95;
    public int DefaultTTLMinutes { get; set; } = 240; // 4 hours
    public int MaxCacheSize { get; set; } = 10000;
    public bool EnableAnalytics { get; set; } = true;
}
