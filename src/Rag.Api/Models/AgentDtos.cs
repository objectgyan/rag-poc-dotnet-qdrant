namespace Rag.Api.Models;

/// <summary>
/// Request to chat with the agent.
/// </summary>
public record AgentChatRequest(
    string Message,
    List<AgentMessageDto>? ConversationHistory = null,
    AgentConfigDto? Config = null
);

/// <summary>
/// Agent message DTO.
/// </summary>
public record AgentMessageDto(
    string Role,
    string? Content = null,
    ToolCallDto? ToolCall = null,
    ToolResultDto? ToolResult = null
);

/// <summary>
/// Tool call DTO.
/// </summary>
public record ToolCallDto(
    string ToolName,
    Dictionary<string, object> Arguments,
    string? ReasoningTrace = null
);

/// <summary>
/// Tool result DTO.
/// </summary>
public record ToolResultDto(
    bool Success,
    string? Content = null,
    Dictionary<string, object>? Data = null,
    string? Error = null
);

/// <summary>
/// Agent configuration DTO.
/// </summary>
public record AgentConfigDto(
    int MaxToolCalls = 5,
    bool AllowParallelToolCalls = true,
    bool UseRagForContext = true,
    int TopKDocuments = 3,
    double MinRelevanceScore = 0.7,
    bool EnableChainOfThought = true,
    string? SystemPrompt = null
);

/// <summary>
/// Agent response DTO.
/// </summary>
public record AgentChatResponse(
    string Answer,
    List<ToolCallDto> ToolCalls,
    List<string> RetrievedDocuments,
    AgentMetricsDto Metrics
);

/// <summary>
/// Agent metrics DTO.
/// </summary>
public record AgentMetricsDto(
    int ToolCallsCount,
    int DocumentsRetrieved,
    double DurationMs,
    double EstimatedCost,
    Dictionary<string, int> ToolUsageCounts
);

/// <summary>
/// Tool information DTO.
/// </summary>
public record ToolInfoDto(
    string Name,
    string Description,
    string Category,
    List<ToolParameterDto> Parameters
);

/// <summary>
/// Tool parameter DTO.
/// </summary>
public record ToolParameterDto(
    string Name,
    string Description,
    string Type,
    bool Required,
    object? DefaultValue = null
);

/// <summary>
/// Request to ingest a codebase.
/// </summary>
public record IngestCodebaseRequest(
    string DirectoryPath,
    List<string>? IncludePatterns = null,
    List<string>? ExcludePatterns = null,
    bool ParseSemanticStructure = true,
    int ChunkSize = 1000
);

/// <summary>
/// Codebase ingestion result DTO.
/// </summary>
public record CodebaseIngestionResultDto(
    int TotalFiles,
    int TotalLines,
    int ChunksCreated,
    List<string> FilesProcessed,
    int ExtractedElements,
    double DurationSeconds,
    string? Error = null
);

/// <summary>
/// Request to search code.
/// </summary>
public record CodeSearchRequest(
    string Query,
    int TopK = 5
);

/// <summary>
/// Code search result DTO.
/// </summary>
public record CodeSearchResultDto(
    string FilePath,
    string CodeSnippet,
    double RelevanceScore
);
