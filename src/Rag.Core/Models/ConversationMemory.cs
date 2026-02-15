namespace Rag.Core.Models;

/// <summary>
/// Represents a memory item stored from conversations
/// </summary>
public class ConversationMemory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string Content { get; set; } = "";
    public MemoryType Type { get; set; } = MemoryType.Fact;
    public string Category { get; set; } = "";
    public int Importance { get; set; } = 5; // 1-10 scale
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    public int AccessCount { get; set; } = 0;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Type of memory being stored
/// </summary>
public enum MemoryType
{
    Fact,          // "User prefers dark mode"
    Preference,    // "Likes detailed explanations"
    Task,          // "Working on RAG system"
    Context,       // "Current project is e-commerce"
    Goal,          // "Learn about vector databases"
    Conversation   // General conversation context
}

/// <summary>
/// Result of a memory search operation
/// </summary>
public class MemorySearchResult
{
    public ConversationMemory Memory { get; set; } = new();
    public double RelevanceScore { get; set; }
}

/// <summary>
/// Settings for conversation memory
/// </summary>
public class MemorySettings
{
    public bool Enabled { get; set; } = true;
    public string Collection { get; set; } = "conversation_memory";
    public int MaxMemoriesPerUser { get; set; } = 1000;
    public int DefaultTTLDays { get; set; } = 30;
    public bool AutoPrune { get; set; } = true;
    public int MinImportanceToKeep { get; set; } = 3;
    public int SearchTopK { get; set; } = 10;
    public double MinRelevanceScore { get; set; } = 0.7;
}
