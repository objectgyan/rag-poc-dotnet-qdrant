using Rag.Core.Models;

namespace Rag.Core.Abstractions;

/// <summary>
/// Service for storing and retrieving conversation memories
/// </summary>
public interface IConversationMemory
{
    /// <summary>
    /// Store a new memory
    /// </summary>
    Task<string> StoreAsync(
        string content, 
        string userId, 
        string tenantId,
        MemoryType type = MemoryType.Fact,
        string category = "",
        int importance = 5,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve a specific memory by ID
    /// </summary>
    Task<ConversationMemory?> GetAsync(
        string memoryId,
        string userId,
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for relevant memories using semantic similarity
    /// </summary>
    Task<List<MemorySearchResult>> SearchAsync(
        string query,
        string userId,
        string tenantId,
        int topK = 10,
        MemoryType? typeFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all memories for a user (paginated)
    /// </summary>
    Task<List<ConversationMemory>> GetAllAsync(
        string userId,
        string tenantId,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a specific memory
    /// </summary>
    Task<bool> DeleteAsync(
        string memoryId,
        string userId,
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete all memories for a user
    /// </summary>
    Task<int> ClearAsync(
        string userId,
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update memory access statistics
    /// </summary>
    Task UpdateAccessAsync(
        string memoryId,
        string userId,
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Prune old or low-importance memories
    /// </summary>
    Task<int> PruneAsync(
        string userId,
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get memory statistics for a user
    /// </summary>
    Task<MemoryStats> GetStatsAsync(
        string userId,
        string tenantId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about user's memories
/// </summary>
public class MemoryStats
{
    public int TotalCount { get; set; }
    public Dictionary<MemoryType, int> CountByType { get; set; } = new();
    public DateTime? OldestMemory { get; set; }
    public DateTime? NewestMemory { get; set; }
    public int TotalAccessCount { get; set; }
    public double AverageImportance { get; set; }
}
