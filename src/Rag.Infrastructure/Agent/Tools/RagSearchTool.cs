using Rag.Core.Abstractions;
using Rag.Core.Agent;

namespace Rag.Infrastructure.Agent.Tools;

/// <summary>
/// Tool for performing RAG-based document search.
/// </summary>
public class RagSearchTool : ITool
{
    private readonly IEmbeddingModel _embeddingModel;
    private readonly IVectorStore _vectorStore;

    public string Name => "rag_search";

    public string Description => "Search through ingested documents using semantic similarity. Returns relevant document chunks for a given query.";

    public IReadOnlyList<ToolParameter> Parameters => new List<ToolParameter>
    {
        new("query", "The search query or question", "string", true),
        new("top_k", "Number of results to return (default: 3)", "number", false, 3),
        new("tenant_id", "Tenant ID for multi-tenancy isolation", "string", false)
    };

    public RagSearchTool(IEmbeddingModel embeddingModel, IVectorStore vectorStore)
    {
        _embeddingModel = embeddingModel;
        _vectorStore = vectorStore;
    }

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
    {
        var query = arguments["query"].ToString()!;
        var topK = arguments.TryGetValue("top_k", out var topKObj) ? Convert.ToInt32(topKObj) : 3;
        var tenantId = arguments.TryGetValue("tenant_id", out var tenantObj) ? tenantObj.ToString() : null;

        // Embed query
        var embeddingResult = await _embeddingModel.EmbedAsync(query, cancellationToken);
        var embedding = embeddingResult.Embedding; // Keep as float[]

        // Search
        var results = await _vectorStore.SearchAsync("rag_collection", embedding, topK, tenantId, cancellationToken);

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

        return ToolResult.Ok(
            content.Trim(),
            new Dictionary<string, object>
            {
                ["query"] = query,
                ["results_count"] = documents.Count,
                ["documents"] = documents
            }
        );
    }
}
