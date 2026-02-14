using Rag.Core.Abstractions;
using Rag.Core.Agent;
using Rag.Core.Text;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Rag.Infrastructure.Agent;

/// <summary>
/// Service for ingesting and indexing codebases.
/// </summary>
public class CodebaseIngestionService : ICodebaseIngestionService
{
    private readonly IEmbeddingModel _embeddingModel;
    private readonly IVectorStore _vectorStore;

    public CodebaseIngestionService(IEmbeddingModel embeddingModel, IVectorStore vectorStore)
    {
        _embeddingModel = embeddingModel;
        _vectorStore = vectorStore;
    }

    public async Task<CodebaseIngestionResult> IngestDirectoryAsync(
        string directoryPath,
        CodebaseIngestionConfig config,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var filesProcessed = new List<string>();
        var extractedElements = new List<CodeElement>();
        var chunksCreated = 0;
        var totalLines = 0;

        if (!Directory.Exists(directoryPath))
        {
            return new CodebaseIngestionResult(0, 0, 0, filesProcessed, extractedElements, stopwatch.Elapsed, $"Directory not found: {directoryPath}");
        }

        // Get all files matching patterns
        var files = GetMatchingFiles(directoryPath, config);

        foreach (var filePath in files)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);

                // Check file size
                if (fileInfo.Length > config.MaxFileSizeKB * 1024)
                {
                    continue; // Skip large files
                }

                var content = await File.ReadAllTextAsync(filePath, cancellationToken);
                var lines = content.Split('\n').Length;
                totalLines += lines;

                // Extract code elements
                var language = DetectLanguage(filePath);
                var elements = config.ParseSemanticStructure
                    ? ExtractCodeElements(content, filePath, language)
                    : new List<CodeElement>();

                extractedElements.AddRange(elements);

                // Chunk the file
                var chunks = ChunkCode(content, filePath, config);

                // Embed and store chunks
                var records = new List<Core.Models.VectorRecord>();
                foreach (var (chunk, idx) in chunks.Select((c, i) => (c, i)))
                {
                    var embeddingResult = await _embeddingModel.EmbedAsync(chunk, cancellationToken);
                    var embedding = embeddingResult.Embedding; // Keep as float[]

                    var metadata = new Dictionary<string, object>
                    {
                        ["type"] = "code",
                        ["documentId"] = $"code:{Path.GetFileName(filePath)}",
                        ["filePath"] = filePath,
                        ["chunkIndex"] = idx,
                        ["text"] = chunk,
                        ["language"] = language
                    };

                    var pointId = Guid.NewGuid().ToString();
                    records.Add(new Core.Models.VectorRecord(pointId, embedding, metadata));
                    chunksCreated++;
                }

                // Upsert all records for this file
                if (records.Count > 0)
                {
                    await _vectorStore.UpsertAsync("rag_collection", records, cancellationToken);
                }

                filesProcessed.Add(filePath);
            }
            catch (Exception ex)
            {
                // Log error but continue processing other files
                Console.WriteLine($"Error processing {filePath}: {ex.Message}");
            }
        }

        stopwatch.Stop();

        return new CodebaseIngestionResult(
            filesProcessed.Count,
            totalLines,
            chunksCreated,
            filesProcessed,
            extractedElements,
            stopwatch.Elapsed
        );
    }

    public async Task<CodebaseIngestionResult> IngestGitHubRepoAsync(
        string owner,
        string repo,
        string? branch = null,
        CodebaseIngestionConfig? config = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        // For now, return not implemented
        // Full implementation would use GitHub API to fetch repo contents
        return new CodebaseIngestionResult(
            0, 0, 0,
            new List<string>(),
            new List<CodeElement>(),
            TimeSpan.Zero,
            "GitHub ingestion not yet implemented. Use local directory ingestion for now."
        );
    }

    public async Task<List<CodeSearchResult>> SearchCodeAsync(
        string query,
        string? tenantId = null,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        var embeddingResult = await _embeddingModel.EmbedAsync(query, cancellationToken);
        var embedding = embeddingResult.Embedding; // Keep as float[]

        var results = await _vectorStore.SearchAsync("rag_collection", embedding, topK, tenantId, cancellationToken);

        return results
            .Where(r => r.Payload.TryGetValue("type", out var type) && type.ToString() == "code")
            .Select(r =>
            {
                r.Payload.TryGetValue("filePath", out var filePath);
                r.Payload.TryGetValue("text", out var text);
                r.Payload.TryGetValue("chunkIndex", out var chunkIndex);

                return new CodeSearchResult(
                    filePath?.ToString() ?? "unknown",
                    0, // Would need to parse for actual line numbers
                    0,
                    text?.ToString() ?? "",
                    null,
                    r.Score
                );
            })
            .ToList();
    }

    public async Task<CodeContext?> GetCodeContextAsync(
        string filePath,
        int? startLine = null,
        int? endLine = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var language = DetectLanguage(filePath);
        var elements = ExtractCodeElements(content, filePath, language);

        // If line range specified, extract that portion
        if (startLine.HasValue && endLine.HasValue)
        {
            var lines = content.Split('\n');
            var selectedLines = lines
                .Skip(startLine.Value - 1)
                .Take(endLine.Value - startLine.Value + 1);
            content = string.Join('\n', selectedLines);
        }

        return new CodeContext(
            filePath,
            content,
            startLine ?? 1,
            endLine ?? content.Split('\n').Length,
            elements,
            language
        );
    }

    private List<string> GetMatchingFiles(string directory, CodebaseIngestionConfig config)
    {
        var allFiles = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories);

        return allFiles.Where(file =>
        {
            var relativePath = Path.GetRelativePath(directory, file);

            // Check include patterns
            var included = config.IncludePatterns == null || config.IncludePatterns.Count == 0 ||
                           config.IncludePatterns.Any(pattern => MatchesPattern(relativePath, pattern));

            if (!included) return false;

            // Check exclude patterns
            var excluded = config.ExcludePatterns != null &&
                           config.ExcludePatterns.Any(pattern => MatchesPattern(relativePath, pattern));

            return !excluded;
        }).ToList();
    }

    private bool MatchesPattern(string path, string pattern)
    {
        // Simple pattern matching (supports *.ext and */folder/*)
        var regexPattern = Regex.Escape(pattern)
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".");

        return Regex.IsMatch(path, regexPattern, RegexOptions.IgnoreCase);
    }

    private string DetectLanguage(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".cs" => "csharp",
            ".py" => "python",
            ".js" => "javascript",
            ".ts" => "typescript",
            ".java" => "java",
            ".go" => "go",
            ".rb" => "ruby",
            ".cpp" or ".cc" or ".cxx" => "cpp",
            ".c" => "c",
            ".rs" => "rust",
            ".php" => "php",
            ".swift" => "swift",
            ".kt" => "kotlin",
            _ => "unknown"
        };
    }

    private List<CodeElement> ExtractCodeElements(string content, string filePath, string language)
    {
        var elements = new List<CodeElement>();

        // Simple regex-based extraction (basic implementation)
        // Production version would use proper parsers (Roslyn for C#, etc.)

        if (language == "csharp")
        {
            // Extract classes
            var classMatches = Regex.Matches(content, @"(?:public|private|internal|protected)?\s*(?:static|abstract|sealed)?\s*class\s+(\w+)");
            foreach (Match match in classMatches)
            {
                var lineNumber = content.Substring(0, match.Index).Count(c => c == '\n') + 1;
                elements.Add(new CodeElement(
                    match.Groups[1].Value,
                    CodeElementType.Class,
                    filePath,
                    lineNumber,
                    lineNumber
                ));
            }

            // Extract methods
            var methodMatches = Regex.Matches(content, @"(?:public|private|internal|protected)\s+(?:static\s+)?(?:async\s+)?(?:\w+(?:<[\w,\s]+>)?)\s+(\w+)\s*\(([^)]*)\)");
            foreach (Match match in methodMatches)
            {
                var lineNumber = content.Substring(0, match.Index).Count(c => c == '\n') + 1;
                elements.Add(new CodeElement(
                    match.Groups[1].Value,
                    CodeElementType.Method,
                    filePath,
                    lineNumber,
                    lineNumber,
                    Signature: match.Value.Trim()
                ));
            }
        }
        else if (language == "python")
        {
            // Extract classes
            var classMatches = Regex.Matches(content, @"class\s+(\w+)(?:\([^)]*\))?:");
            foreach (Match match in classMatches)
            {
                var lineNumber = content.Substring(0, match.Index).Count(c => c == '\n') + 1;
                elements.Add(new CodeElement(
                    match.Groups[1].Value,
                    CodeElementType.Class,
                    filePath,
                    lineNumber,
                    lineNumber
                ));
            }

            // Extract functions
            var funcMatches = Regex.Matches(content, @"def\s+(\w+)\s*\(([^)]*)\)(?:\s*->\s*[\w\[\],\s]+)?:");
            foreach (Match match in funcMatches)
            {
                var lineNumber = content.Substring(0, match.Index).Count(c => c == '\n') + 1;
                elements.Add(new CodeElement(
                    match.Groups[1].Value,
                    CodeElementType.Function,
                    filePath,
                    lineNumber,
                    lineNumber,
                    Signature: match.Value.Trim()
                ));
            }
        }

        return elements;
    }

    private List<string> ChunkCode(string content, string filePath, CodebaseIngestionConfig config)
    {
        // Simple chunking by character count (production would use Chunker.ChunkByTokens)
        var chunks = new List<string>();
        for (int i = 0; i < content.Length; i += config.ChunkSize - config.ChunkOverlap)
        {
            var length = Math.Min(config.ChunkSize, content.Length - i);
            chunks.Add(content.Substring(i, length));
        }

        // Add file path context to each chunk
        return chunks.Select(chunk => $"File: {Path.GetFileName(filePath)}\n\n{chunk}").ToList();
    }
}
