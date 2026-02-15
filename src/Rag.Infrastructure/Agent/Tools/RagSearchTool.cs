using Rag.Core.Abstractions;
using Rag.Core.Agent;
using Rag.Core.Models;
using Microsoft.Extensions.Logging;

namespace Rag.Infrastructure.Agent.Tools;

/// <summary>
/// Tool for performing RAG-based document search with semantic caching.
/// </summary>
public class RagSearchTool : ITool
{
    private readonly IEmbeddingModel _embeddingModel;
    private readonly IVectorStore _vectorStore;
    private readonly ISemanticCache? _semanticCache;
    private readonly ILogger<RagSearchTool>? _logger;
    private readonly string _collectionName;

    public string Name => "rag_search";

    public string Description => "Search through ingested documents using semantic similarity. Returns relevant document chunks for a given query.";

    public IReadOnlyList<ToolParameter> Parameters => new List<ToolParameter>
    {
        new("query", "The search query or question", "string", true),
        new("top_k", "Number of results to return (default: 3)", "number", false, 3),
        new("tenant_id", "Tenant ID for multi-tenancy isolation", "string", false)
    };

    public RagSearchTool(
        IEmbeddingModel embeddingModel, 
        IVectorStore vectorStore, 
        QdrantSettings qdrantSettings,
        ISemanticCache? semanticCache = null,
        ILogger<RagSearchTool>? logger = null)
    {
        _embeddingModel = embeddingModel;
        _vectorStore = vectorStore;
        _semanticCache = semanticCache;
        _logger = logger;
        _collectionName = qdrantSettings.Collection;
    }

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
    {
        var query = arguments["query"].ToString()!;
        var topK = arguments.TryGetValue("top_k", out var topKObj) ? Convert.ToInt32(topKObj) : 3;
        var tenantId = arguments.TryGetValue("tenant_id", out var tenantObj) ? tenantObj.ToString() : "default";

        // Check semantic cache first
        if (_semanticCache != null)
        {
            var cachedResult = await _semanticCache.GetSimilarAsync(query, tenantId, cancellationToken);
            if (cachedResult != null)
            {
                _logger?.LogInformation("RagSearchTool: Cache HIT for query '{Query}' (similarity: {Similarity:F3})", query, cachedResult.SimilarityScore);
                
                // Parse cached response to extract metadata
                var resultsCount = cachedResult.Citations?.Count ?? 0;
                var cachedDocuments = cachedResult.Citations?.Select((c, idx) => new
                {
                    rank = idx + 1,
                    document_id = c.DocumentId,
                    score = c.Score,
                    chunk_index = c.ChunkIndex
                }).ToList();

                return ToolResult.Ok(
                    cachedResult.Response,
                    new Dictionary<string, object>
                    {
                        ["query"] = query,
                        ["results_count"] = resultsCount,
                        ["documents"] = (object?)cachedDocuments ?? new List<object>(),
                        ["cached"] = true,
                        ["cache_similarity"] = cachedResult.SimilarityScore
                    }
                );
            }
            else
            {
                _logger?.LogInformation("RagSearchTool: Cache MISS for query '{Query}' - performing full search", query);
            }
        }

        // Embed query
        var embeddingResult = await _embeddingModel.EmbedAsync(query, cancellationToken);
        var embedding = embeddingResult.Embedding; // Keep as float[]

        // Search using configured collection
        var results = await _vectorStore.SearchAsync(_collectionName, embedding, topK, tenantId, cancellationToken);

        if (results.Count == 0)
        {
            return ToolResult.Ok(
                "No relevant documents found.",
                new Dictionary<string, object>
                {
                    ["query"] = query,
                    ["results_count"] = 0
                }
            );
        }

        // Format results
        var documents = results.Select((r, idx) =>
        {
            r.Payload.TryGetValue("text", out var text);
            r.Payload.TryGetValue("documentId", out var docId);
            r.Payload.TryGetValue("pageNumber", out var page);

            return new
            {
                rank = idx + 1,
                document_id = docId?.ToString() ?? "unknown",
                page = page?.ToString(),
                score = r.Score,
                text = text?.ToString() ?? ""
            };
        }).ToList();

        var content = $"Found {results.Count} relevant document(s):\n\n";
        foreach (var doc in documents)
        {
            content += $"[{doc.rank}] Document: {doc.document_id}";
            if (doc.page != null) content += $" (Page {doc.page})";
            content += $"\nRelevance: {doc.score:F3}\nContent: {doc.text}\n\n";
        }

        // Store in semantic cache
        if (_semanticCache != null)
        {
            var citations = documents.Select(d => new Citation(
                d.document_id,
                0, // chunk index not available in current payload
                d.score
            )).ToList();

            await _semanticCache.StoreAsync(
                query,
                content.Trim(),
                citations,
                tenantId,
                new TokenUsage(), // Tool doesn't track token usage directly
                cancellationToken
            );
        }

        return ToolResult.Ok(
            content.Trim(),
            new Dictionary<string, object>
            {
                ["query"] = query,
                ["results_count"] = documents.Count,
                ["documents"] = documents,
                ["cached"] = false
            }
        );
    }
}
