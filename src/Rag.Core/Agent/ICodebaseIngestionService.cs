namespace Rag.Core.Agent;

/// <summary>
/// Service for ingesting and indexing codebases for AI-powered code understanding.
/// </summary>
public interface ICodebaseIngestionService
{
    /// <summary>
    /// Ingest a local codebase directory.
    /// </summary>
    Task<CodebaseIngestionResult> IngestDirectoryAsync(
        string directoryPath,
        CodebaseIngestionConfig config,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ingest a GitHub repository.
    /// </summary>
    Task<CodebaseIngestionResult> IngestGitHubRepoAsync(
        string owner,
        string repo,
        string? branch = null,
        CodebaseIngestionConfig? config = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for code snippets in ingested codebases.
    /// </summary>
    Task<List<CodeSearchResult>> SearchCodeAsync(
        string query,
        string? tenantId = null,
        int topK = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get code context for a specific file and line range.
    /// </summary>
    Task<CodeContext?> GetCodeContextAsync(
        string filePath,
        int? startLine = null,
        int? endLine = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration for codebase ingestion.
/// </summary>
public record CodebaseIngestionConfig(
    List<string>? IncludePatterns = null,      // e.g., ["*.cs", "*.py"]
    List<string>? ExcludePatterns = null,      // e.g., ["*/bin/*", "*/obj/*"]
    bool IncludeComments = true,
    bool IncludeDocstrings = true,
    int MaxFileSizeKB = 1024,
    bool ParseSemanticStructure = true,        // Extract classes, functions, etc.
    int ChunkSize = 1000,
    int ChunkOverlap = 200
)
{
    public static CodebaseIngestionConfig Default => new(
        IncludePatterns: new List<string> { "*.cs", "*.py", "*.js", "*.ts", "*.java", "*.go", "*.rb" },
        ExcludePatterns: new List<string> { "*/bin/*", "*/obj/*", "*/node_modules/*", "*/.git/*", "*/dist/*", "*/build/*" }
    );
}

/// <summary>
/// Result of codebase ingestion.
/// </summary>
public record CodebaseIngestionResult(
    int TotalFiles,
    int TotalLines,
    int ChunksCreated,
    List<string> FilesProcessed,
    List<CodeElement> ExtractedElements,
    TimeSpan Duration,
    string? Error = null
);

/// <summary>
/// Represents a code element (class, function, method, etc.).
/// </summary>
public record CodeElement(
    string Name,
    CodeElementType Type,
    string FilePath,
    int StartLine,
    int EndLine,
    string? Signature = null,
    string? Documentation = null,
    List<string>? Parameters = null,
    string? ReturnType = null
);

/// <summary>
/// Type of code element.
/// </summary>
public enum CodeElementType
{
    Class,
    Interface,
    Method,
    Function,
    Property,
    Field,
    Enum,
    Constant
}

/// <summary>
/// Search result for code queries.
/// </summary>
public record CodeSearchResult(
    string FilePath,
    int StartLine,
    int EndLine,
    string CodeSnippet,
    string? Context,
    double RelevanceScore,
    CodeElement? Element = null
);

/// <summary>
/// Code context with surrounding information.
/// </summary>
public record CodeContext(
    string FilePath,
    string Content,
    int StartLine,
    int EndLine,
    List<CodeElement> RelatedElements,
    string? FileLanguage = null
);
