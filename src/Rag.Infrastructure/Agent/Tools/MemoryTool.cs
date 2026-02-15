using Rag.Core.Abstractions;
using Rag.Core.Agent;
using Rag.Core.Models;
using System.Text.Json;

namespace Rag.Infrastructure.Agent.Tools;

/// <summary>
/// Tool for storing and retrieving conversation memories.
/// </summary>
public class MemoryTool : ITool
{
    private readonly IConversationMemory _memory;

    public string Name => "memory";

    public string Description => "Store and retrieve information from conversation history across all sessions. Memory is shared tenant-wide - information stored here persists across conversations and can be retrieved later. Use this to remember important user preferences, facts about the user, ongoing tasks, and context.";

    public IReadOnlyList<ToolParameter> Parameters => new List<ToolParameter>
    {
        new("action", "Action to perform: 'store', 'search', 'get_all', 'stats', 'clear'", "string", true, null, new[] { "store", "search", "get_all", "stats", "clear" }),
        new("content", "Content to store or search query (required for 'store' and 'search')", "string", false),
        new("tenant_id", "Tenant identifier (defaults to current tenant)", "string", false),
        new("type", "Memory type: 'fact', 'preference', 'task', 'context', 'goal', 'conversation'", "string", false, "fact", new[] { "fact", "preference", "task", "context", "goal", "conversation" }),
        new("category", "Memory category for organization (e.g., 'coding', 'preferences', 'personal')", "string", false),
        new("importance", "Importance level (1-10, default: 5)", "number", false, 5),
        new("top_k", "Number of memories to return for search (default: 10)", "number", false, 10)
    };

    public MemoryTool(IConversationMemory memory)
    {
        _memory = memory;
    }

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var action = arguments["action"].ToString()!.ToLower();
            var tenantId = arguments.TryGetValue("tenant_id", out var tid) ? tid.ToString()! : "default";
            
            // Use tenant-wide memory: all conversations in the same tenant share memory
            // This allows cross-session memory retrieval
            var userId = $"tenant-user-{tenantId}";

            return action switch
            {
                "store" => await HandleStoreAsync(arguments, userId, tenantId, cancellationToken),
                "search" => await HandleSearchAsync(arguments, userId, tenantId, cancellationToken),
                "get_all" => await HandleGetAllAsync(userId, tenantId, cancellationToken),
                "stats" => await HandleStatsAsync(userId, tenantId, cancellationToken),
                "clear" => await HandleClearAsync(userId, tenantId, cancellationToken),
                _ => ToolResult.Fail($"Unknown action: {action}. Valid actions: store, search, get_all, stats, clear")
            };
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Memory tool error: {ex.Message}");
        }
    }

    private async Task<ToolResult> HandleStoreAsync(
        Dictionary<string, object> arguments,
        string userId,
        string tenantId,
        CancellationToken cancellationToken)
    {
        if (!arguments.TryGetValue("content", out var contentObj) || string.IsNullOrWhiteSpace(contentObj?.ToString()))
        {
            return ToolResult.Fail("Content is required for 'store' action");
        }

        var content = contentObj.ToString()!;
        var type = arguments.TryGetValue("type", out var typeObj) && Enum.TryParse<MemoryType>(typeObj.ToString(), true, out var memType)
            ? memType
            : MemoryType.Fact;
        var category = arguments.TryGetValue("category", out var catObj) ? catObj.ToString()! : "";
        var importance = arguments.TryGetValue("importance", out var impObj) ? Convert.ToInt32(impObj) : 5;

        var memoryId = await _memory.StoreAsync(
            content,
            userId,
            tenantId,
            type,
            category,
            importance,
            null,
            cancellationToken);

        return ToolResult.Ok(
            $"Memory stored successfully. ID: {memoryId}",
            new Dictionary<string, object>
            {
                ["memory_id"] = memoryId,
                ["content"] = content,
                ["type"] = type.ToString(),
                ["importance"] = importance
            });
    }

    private async Task<ToolResult> HandleSearchAsync(
        Dictionary<string, object> arguments,
        string userId,
        string tenantId,
        CancellationToken cancellationToken)
    {
        if (!arguments.TryGetValue("content", out var queryObj) || string.IsNullOrWhiteSpace(queryObj?.ToString()))
        {
            return ToolResult.Fail("Content (query) is required for 'search' action");
        }

        var query = queryObj.ToString()!;
        var topK = arguments.TryGetValue("top_k", out var topKObj) ? Convert.ToInt32(topKObj) : 10;
        var typeFilter = arguments.TryGetValue("type", out var typeObj) && Enum.TryParse<MemoryType>(typeObj.ToString(), true, out var memType)
            ? (MemoryType?)memType
            : null;

        var results = await _memory.SearchAsync(query, userId, tenantId, topK, typeFilter, cancellationToken);

        if (results.Count == 0)
        {
            return ToolResult.Ok(
                "No relevant memories found.",
                new Dictionary<string, object>
                {
                    ["query"] = query,
                    ["results_count"] = 0
                });
        }

        var memories = results.Select((r, idx) => new
        {
            rank = idx + 1,
            memory_id = r.Memory.Id,
            content = r.Memory.Content,
            type = r.Memory.Type.ToString(),
            category = r.Memory.Category,
            importance = r.Memory.Importance,
            relevance = r.RelevanceScore,
            created = r.Memory.CreatedAt,
            access_count = r.Memory.AccessCount
        }).ToList();

        var content = $"Found {results.Count} relevant memory/memories:\n\n";
        foreach (var mem in memories)
        {
            content += $"[{mem.rank}] {mem.type}";
            if (!string.IsNullOrEmpty(mem.category)) content += $" ({mem.category})";
            content += $"\nRelevance: {mem.relevance:F3} | Importance: {mem.importance}/10\n";
            content += $"Content: {mem.content}\n";
            content += $"Created: {mem.created:g} | Accessed {mem.access_count} times\n\n";
        }

        return ToolResult.Ok(
            content.Trim(),
            new Dictionary<string, object>
            {
                ["query"] = query,
                ["results_count"] = memories.Count,
                ["memories"] = memories
            });
    }

    private async Task<ToolResult> HandleGetAllAsync(
        string userId,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var memories = await _memory.GetAllAsync(userId, tenantId, 0, 50, cancellationToken);

        if (memories.Count == 0)
        {
            return ToolResult.Ok(
                "No memories found for this user.",
                new Dictionary<string, object> { ["count"] = 0 });
        }

        var memoryList = memories.Select(m => new
        {
            id = m.Id,
            content = m.Content.Length > 100 ? m.Content.Substring(0, 100) + "..." : m.Content,
            type = m.Type.ToString(),
            category = m.Category,
            importance = m.Importance,
            created = m.CreatedAt,
            access_count = m.AccessCount
        }).ToList();

        var content = $"Total memories: {memories.Count}\n\n";
        foreach (var mem in memoryList.Take(10))
        {
            content += $"• {mem.type}";
            if (!string.IsNullOrEmpty(mem.category)) content += $" ({mem.category})";
            content += $" | Importance: {mem.importance}/10\n";
            content += $"  {mem.content}\n";
            content += $"  Created: {mem.created:g} | Accessed {mem.access_count} times\n\n";
        }

        if (memories.Count > 10)
        {
            content += $"... and {memories.Count - 10} more memories (showing first 10)";
        }

        return ToolResult.Ok(
            content.Trim(),
            new Dictionary<string, object>
            {
                ["total_count"] = memories.Count,
                ["showing"] = Math.Min(10, memories.Count),
                ["memories"] = memoryList
            });
    }

    private async Task<ToolResult> HandleStatsAsync(
        string userId,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var stats = await _memory.GetStatsAsync(userId, tenantId, cancellationToken);

        if (stats.TotalCount == 0)
        {
            return ToolResult.Ok(
                "No memory statistics available (no memories stored).",
                new Dictionary<string, object> { ["total_count"] = 0 });
        }

        var content = $"Memory Statistics for {userId}:\n\n";
        content += $"Total Memories: {stats.TotalCount}\n";
        content += $"Average Importance: {stats.AverageImportance:F1}/10\n";
        content += $"Total Accesses: {stats.TotalAccessCount}\n";
        content += $"Oldest Memory: {stats.OldestMemory:g}\n";
        content += $"Newest Memory: {stats.NewestMemory:g}\n\n";
        content += "By Type:\n";
        foreach (var typeCount in stats.CountByType.OrderByDescending(kvp => kvp.Value))
        {
            content += $"  • {typeCount.Key}: {typeCount.Value}\n";
        }

        return ToolResult.Ok(
            content.Trim(),
            new Dictionary<string, object>
            {
                ["total_count"] = stats.TotalCount,
                ["by_type"] = stats.CountByType,
                ["average_importance"] = stats.AverageImportance,
                ["total_accesses"] = stats.TotalAccessCount
            });
    }

    private async Task<ToolResult> HandleClearAsync(
        string userId,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var count = await _memory.ClearAsync(userId, tenantId, cancellationToken);

        return ToolResult.Ok(
            $"Cleared {count} memories for user {userId}.",
            new Dictionary<string, object>
            {
                ["cleared_count"] = count,
                ["user_id"] = userId
            });
    }
}
