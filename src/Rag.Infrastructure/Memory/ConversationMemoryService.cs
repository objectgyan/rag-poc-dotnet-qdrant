using Microsoft.Extensions.Logging;
using Rag.Core.Abstractions;
using Rag.Core.Models;
using System.Text.Json;

namespace Rag.Infrastructure.Memory;

/// <summary>
/// Conversation memory service using Qdrant for vector storage
/// </summary>
public class ConversationMemoryService : IConversationMemory
{
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingModel _embeddings;
    private readonly MemorySettings _settings;
    private readonly ILogger<ConversationMemoryService> _logger;

    public ConversationMemoryService(
        IVectorStore vectorStore,
        IEmbeddingModel embeddings,
        MemorySettings settings,
        ILogger<ConversationMemoryService> logger)
    {
        _vectorStore = vectorStore;
        _embeddings = embeddings;
        _settings = settings;
        _logger = logger;
    }

    public async Task<string> StoreAsync(
        string content,
        string userId,
        string tenantId,
        MemoryType type = MemoryType.Fact,
        string category = "",
        int importance = 5,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogDebug("Memory storage is disabled");
            return string.Empty;
        }

        try
        {
            var memory = new ConversationMemory
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                TenantId = tenantId,
                Content = content,
                Type = type,
                Category = category,
                Importance = Math.Clamp(importance, 1, 10),
                CreatedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow,
                AccessCount = 0,
                Metadata = metadata ?? new Dictionary<string, string>()
            };

            // Generate embedding for semantic search
            var embeddingResult = await _embeddings.EmbedAsync(content, cancellationToken);

            // Store in Qdrant
            var record = new VectorRecord(
                memory.Id,
                embeddingResult.Embedding,
                new Dictionary<string, object>
                {
                    ["userId"] = userId,
                    ["tenantId"] = tenantId,
                    ["content"] = content,
                    ["type"] = type.ToString(),
                    ["category"] = category,
                    ["importance"] = importance,
                    ["createdAt"] = memory.CreatedAt.ToString("o"),
                    ["lastAccessedAt"] = memory.LastAccessedAt.ToString("o"),
                    ["accessCount"] = 0,
                    ["metadata"] = JsonSerializer.Serialize(metadata ?? new Dictionary<string, string>())
                });

            await _vectorStore.UpsertAsync(_settings.Collection, new[] { record }, cancellationToken);

            _logger.LogInformation(
                "Stored memory {MemoryId} for user {UserId}: {Type} - {Content}",
                memory.Id, userId, type, content.Length > 50 ? content.Substring(0, 50) + "..." : content);

            // Auto-prune if needed
            if (_settings.AutoPrune)
            {
                await PruneAsync(userId, tenantId, cancellationToken);
            }

            return memory.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store memory for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<MemorySearchResult>> SearchAsync(
        string query,
        string userId,
        string tenantId,
        int topK = 10,
        MemoryType? typeFilter = null,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return new List<MemorySearchResult>();
        }

        try
        {
            // Generate query embedding
            var embeddingResult = await _embeddings.EmbedAsync(query, cancellationToken);

            // Search Qdrant with user/tenant filtering
            var hits = await _vectorStore.SearchAsync(
                _settings.Collection,
                embeddingResult.Embedding,
                topK,
                tenantId,
                cancellationToken);

            var results = new List<MemorySearchResult>();

            foreach (var hit in hits)
            {
                // Additional filtering by userId and optionally by type
                var hitUserId = hit.Payload.TryGetValue("userId", out var uid) ? uid?.ToString() : "";
                if (hitUserId != userId)
                    continue;

                var hitType = hit.Payload.TryGetValue("type", out var t) 
                    ? Enum.Parse<MemoryType>(t?.ToString() ?? "Fact") 
                    : MemoryType.Fact;

                if (typeFilter.HasValue && hitType != typeFilter.Value)
                    continue;

                // Only return memories above relevance threshold
                if (hit.Score < _settings.MinRelevanceScore)
                    continue;

                var memory = HitToMemory(hit);
                results.Add(new MemorySearchResult
                {
                    Memory = memory,
                    RelevanceScore = hit.Score
                });

                // Update access statistics asynchronously
                _ = Task.Run(() => UpdateAccessAsync(memory.Id, userId, tenantId, cancellationToken), cancellationToken);
            }

            _logger.LogInformation(
                "Found {Count} relevant memories for query '{Query}' (user: {UserId})",
                results.Count, query.Length > 50 ? query.Substring(0, 50) + "..." : query, userId);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search memories for user {UserId}", userId);
            return new List<MemorySearchResult>();
        }
    }

    public async Task<ConversationMemory?> GetAsync(
        string memoryId,
        string userId,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        // Note: Qdrant doesn't have a direct "get by ID" API in our abstraction
        // We'll search for all user memories and find the matching one
        var allMemories = await GetAllAsync(userId, tenantId, 0, 1000, cancellationToken);
        return allMemories.FirstOrDefault(m => m.Id == memoryId);
    }

    public async Task<List<ConversationMemory>> GetAllAsync(
        string userId,
        string tenantId,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return new List<ConversationMemory>();
        }

        try
        {
            // Search with a zero vector to get all points (not ideal, but works for small datasets)
            // In production, consider using Qdrant's scroll API for better performance
            var zeroVector = new float[1536]; // Assuming text-embedding-3-small dimension
            var hits = await _vectorStore.SearchAsync(
                _settings.Collection,
                zeroVector,
                1000,
                tenantId,
                cancellationToken);

            var memories = hits
                .Select(HitToMemory)
                .Where(m => m.UserId == userId)
                .OrderByDescending(m => m.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToList();

            return memories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all memories for user {UserId}", userId);
            return new List<ConversationMemory>();
        }
    }

    public Task<bool> DeleteAsync(
        string memoryId,
        string userId,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        // Note: Current IVectorStore doesn't have delete functionality
        // This would need to be added to the abstraction
        _logger.LogWarning("Delete operation not yet implemented for memory {MemoryId}", memoryId);
        return Task.FromResult(false);
    }

    public Task<int> ClearAsync(
        string userId,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        // Note: Current IVectorStore doesn't have bulk delete functionality
        // This would need to be added to the abstraction
        _logger.LogWarning("Clear operation not yet implemented for user {UserId}", userId);
        return Task.FromResult(0);
    }

    public async Task UpdateAccessAsync(
        string memoryId,
        string userId,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var memory = await GetAsync(memoryId, userId, tenantId, cancellationToken);
            if (memory == null)
                return;

            memory.AccessCount++;
            memory.LastAccessedAt = DateTime.UtcNow;

            // Re-embed and update (upsert with same ID)
            var embeddingResult = await _embeddings.EmbedAsync(memory.Content, cancellationToken);

            var record = new VectorRecord(
                memory.Id,
                embeddingResult.Embedding,
                MemoryToPayload(memory));

            await _vectorStore.UpsertAsync(_settings.Collection, new[] { record }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update access for memory {MemoryId}", memoryId);
        }
    }

    public async Task<int> PruneAsync(
        string userId,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.AutoPrune)
            return 0;

        try
        {
            var memories = await GetAllAsync(userId, tenantId, 0, 10000, cancellationToken);

            // Calculate which memories to prune
            var toPrune = memories
                .Where(m => m.Importance < _settings.MinImportanceToKeep)
                .Where(m => (DateTime.UtcNow - m.LastAccessedAt).TotalDays > _settings.DefaultTTLDays)
                .Take(memories.Count - _settings.MaxMemoriesPerUser)
                .ToList();

            // In a real implementation, we'd delete these
            _logger.LogInformation(
                "Would prune {Count} memories for user {UserId} (not implemented)",
                toPrune.Count, userId);

            return toPrune.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prune memories for user {UserId}", userId);
            return 0;
        }
    }

    public async Task<MemoryStats> GetStatsAsync(
        string userId,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var memories = await GetAllAsync(userId, tenantId, 0, 10000, cancellationToken);

        if (memories.Count == 0)
        {
            return new MemoryStats();
        }

        return new MemoryStats
        {
            TotalCount = memories.Count,
            CountByType = memories.GroupBy(m => m.Type).ToDictionary(g => g.Key, g => g.Count()),
            OldestMemory = memories.Min(m => m.CreatedAt),
            NewestMemory = memories.Max(m => m.CreatedAt),
            TotalAccessCount = memories.Sum(m => m.AccessCount),
            AverageImportance = memories.Average(m => m.Importance)
        };
    }

    private ConversationMemory HitToMemory(VectorHit hit)
    {
        var metadata = hit.Payload.TryGetValue("metadata", out var m) && m != null
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(m.ToString() ?? "{}")
            : new Dictionary<string, string>();

        return new ConversationMemory
        {
            Id = hit.Id,
            UserId = hit.Payload.TryGetValue("userId", out var uid) ? uid?.ToString() ?? "" : "",
            TenantId = hit.Payload.TryGetValue("tenantId", out var tid) ? tid?.ToString() ?? "" : "",
            Content = hit.Payload.TryGetValue("content", out var content) ? content?.ToString() ?? "" : "",
            Type = hit.Payload.TryGetValue("type", out var type)
                ? Enum.Parse<MemoryType>(type?.ToString() ?? "Fact")
                : MemoryType.Fact,
            Category = hit.Payload.TryGetValue("category", out var cat) ? cat?.ToString() ?? "" : "",
            Importance = hit.Payload.TryGetValue("importance", out var imp) && int.TryParse(imp?.ToString(), out var impVal) ? impVal : 5,
            CreatedAt = hit.Payload.TryGetValue("createdAt", out var created) && DateTime.TryParse(created?.ToString(), out var createdDt) ? createdDt : DateTime.UtcNow,
            LastAccessedAt = hit.Payload.TryGetValue("lastAccessedAt", out var accessed) && DateTime.TryParse(accessed?.ToString(), out var accessedDt) ? accessedDt : DateTime.UtcNow,
            AccessCount = hit.Payload.TryGetValue("accessCount", out var count) && int.TryParse(count?.ToString(), out var countVal) ? countVal : 0,
            Metadata = metadata ?? new Dictionary<string, string>()
        };
    }

    private Dictionary<string, object> MemoryToPayload(ConversationMemory memory)
    {
        return new Dictionary<string, object>
        {
            ["userId"] = memory.UserId,
            ["tenantId"] = memory.TenantId,
            ["content"] = memory.Content,
            ["type"] = memory.Type.ToString(),
            ["category"] = memory.Category,
            ["importance"] = memory.Importance,
            ["createdAt"] = memory.CreatedAt.ToString("o"),
            ["lastAccessedAt"] = memory.LastAccessedAt.ToString("o"),
            ["accessCount"] = memory.AccessCount,
            ["metadata"] = JsonSerializer.Serialize(memory.Metadata)
        };
    }
}
