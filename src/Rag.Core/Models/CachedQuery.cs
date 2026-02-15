using Rag.Core.Models;

namespace Rag.Core.Models;

/// <summary>
/// Represents a cached query with its embedding and metadata.
/// </summary>
public class CachedQuery
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Query { get; set; } = string.Empty;
    public float[] QueryEmbedding { get; set; } = Array.Empty<float>();
    public string Response { get; set; } = string.Empty;
    public List<Citation> Citations { get; set; } = new();
    public string TenantId { get; set; } = "default";
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
    public TokenUsage TokenUsage { get; set; } = new();
    public int AccessCount { get; set; }
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
}
