namespace Rag.Core.Agent;

/// <summary>
/// Orchestrates agent behavior: decides when to use RAG, tools, or both.
/// Implements reasoning and planning capabilities.
/// </summary>
public interface IAgentOrchestrator
{
    /// <summary>
    /// Process a user message and orchestrate the agent response.
    /// May involve RAG retrieval, tool calls, reasoning, etc.
    /// </summary>
    Task<AgentResponse> ProcessAsync(
        string userMessage,
        List<AgentMessage> conversationHistory,
        AgentConfig config,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stream agent response with real-time tool calls and reasoning.
    /// </summary>
    IAsyncEnumerable<AgentStreamChunk> StreamAsync(
        string userMessage,
        List<AgentMessage> conversationHistory,
        AgentConfig config,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Citation from RAG search.
/// </summary>
public record AgentCitation(
    string DocumentId,
    int? PageNumber,
    double Score,
    string? Text = null
);

/// <summary>
/// Agent's response to a user message.
/// </summary>
public record AgentResponse(
    string FinalAnswer,
    List<AgentMessage> Messages,
    List<ToolCall> ToolCallsExecuted,
    List<string> RetrievedDocuments,
    List<AgentCitation> Citations,
    AgentMetrics Metrics
);

/// <summary>
/// Streaming chunk of agent response.
/// </summary>
public record AgentStreamChunk(
    AgentStreamChunkType Type,
    string? Content = null,
    ToolCall? ToolCall = null,
    ToolResult? ToolResult = null,
    string? ReasoningTrace = null
);

/// <summary>
/// Type of streaming chunk.
/// </summary>
public enum AgentStreamChunkType
{
    Reasoning,        // Chain of thought reasoning
    ToolCallStart,    // Starting a tool call
    ToolCallResult,   // Tool call result
    ContentDelta,     // Incremental content generation
    ContentComplete,  // Final content complete
    Error             // Error occurred
}

/// <summary>
/// Metrics about agent execution.
/// </summary>
public record AgentMetrics(
    int ToolCallsCount,
    int DocumentsRetrieved,
    TimeSpan TotalDuration,
    double EstimatedCost,
    Dictionary<string, int> ToolUsageCounts
);
