namespace Rag.Core.Agent;

/// <summary>
/// Represents a tool that can be called by the agent.
/// Inspired by MCP (Model Context Protocol) architecture.
/// </summary>
public interface ITool
{
    /// <summary>
    /// Unique identifier for the tool.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable description of what the tool does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// List of parameters the tool accepts.
    /// </summary>
    IReadOnlyList<ToolParameter> Parameters { get; }

    /// <summary>
    /// Execute the tool with provided arguments.
    /// </summary>
    Task<ToolResult> ExecuteAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines a parameter for a tool.
/// </summary>
public record ToolParameter(
    string Name,
    string Description,
    string Type, // "string", "number", "boolean", "array", "object"
    bool Required,
    object? DefaultValue = null,
    IReadOnlyList<string>? EnumValues = null
);

/// <summary>
/// Result of tool execution.
/// </summary>
public record ToolResult(
    bool Success,
    string? Content = null,
    Dictionary<string, object>? Data = null,
    string? Error = null
)
{
    public static ToolResult Ok(string content, Dictionary<string, object>? data = null)
        => new(Success: true, Content: content, Data: data);

    public static ToolResult Fail(string error)
        => new(Success: false, Error: error);
}

/// <summary>
/// Represents a tool call request from the agent.
/// </summary>
public record ToolCall(
    string ToolName,
    Dictionary<string, object> Arguments,
    string? ReasoningTrace = null
);

/// <summary>
/// Agent message in a conversation.
/// </summary>
public record AgentMessage(
    string Role, // "user", "assistant", "system", "tool"
    string? Content = null,
    ToolCall? ToolCall = null,
    ToolResult? ToolResult = null
);

/// <summary>
/// Configuration for agent behavior.
/// </summary>
public record AgentConfig(
    int MaxToolCalls = 5,
    bool AllowParallelToolCalls = true,
    bool UseRagForContext = true,
    int TopKDocuments = 3,
    double MinRelevanceScore = 0.7,
    bool EnableChainOfThought = true,
    string? SystemPrompt = null
);

/// <summary>
/// Categories of tools available to the agent.
/// </summary>
public enum ToolCategory
{
    RAG,              // Retrieval and document search
    GitHub,           // GitHub API integration
    CodeAnalysis,     // Code parsing and analysis
    WebSearch,        // Web search capabilities
    FileSystem,       // File system operations
    Custom            // User-defined tools
}

/// <summary>
/// Metadata about a tool for discovery and organization.
/// </summary>
public record ToolMetadata(
    string Name,
    string Description,
    ToolCategory Category,
    IReadOnlyList<string> Tags,
    bool RequiresAuth = false,
    string? Version = "1.0.0"
);
