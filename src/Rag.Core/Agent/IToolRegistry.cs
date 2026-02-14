namespace Rag.Core.Agent;

/// <summary>
/// Registry for managing and discovering available tools.
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Register a tool in the registry.
    /// </summary>
    void RegisterTool(ITool tool, ToolMetadata? metadata = null);

    /// <summary>
    /// Get a tool by name.
    /// </summary>
    ITool? GetTool(string name);

    /// <summary>
    /// Get all registered tools.
    /// </summary>
    IReadOnlyList<ITool> GetAllTools();

    /// <summary>
    /// Get tools by category.
    /// </summary>
    IReadOnlyList<ITool> GetToolsByCategory(ToolCategory category);

    /// <summary>
    /// Search tools by tags or description.
    /// </summary>
    IReadOnlyList<ITool> SearchTools(string query);

    /// <summary>
    /// Get metadata for a tool.
    /// </summary>
    ToolMetadata? GetToolMetadata(string toolName);

    /// <summary>
    /// Check if a tool exists.
    /// </summary>
    bool HasTool(string name);
}

/// <summary>
/// Executes tool calls and manages execution context.
/// </summary>
public interface IToolExecutor
{
    /// <summary>
    /// Execute a single tool call.
    /// </summary>
    Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute multiple tool calls in parallel (if config allows).
    /// </summary>
    Task<Dictionary<string, ToolResult>> ExecuteParallelAsync(
        IEnumerable<ToolCall> toolCalls,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate tool call arguments before execution.
    /// </summary>
    (bool IsValid, string? Error) ValidateToolCall(ToolCall toolCall);
}
