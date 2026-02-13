using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Rag.Api.Configuration;
using Rag.Api.Middleware;
using Rag.Api.Models;
using Rag.Core.Abstractions;
using Rag.Core.Models;
using Rag.Core.Services;
using Rag.Core.Text;

namespace Rag.Api.Controllers;

[ApiController]
[Route("ingest")]
[EnableRateLimiting(RateLimitingConfiguration.IngestPolicy)]
public sealed class IngestController : ControllerBase
{
    private readonly IEmbeddingModel _embeddings;
    private readonly IVectorStore _vectorStore;
    private readonly QdrantSettings _qdrant;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<IngestController> _log;

    public IngestController(
        IEmbeddingModel embeddings, 
        IVectorStore vectorStore, 
        QdrantSettings qdrant, 
        ITenantContext tenantContext,
        ILogger<IngestController> log)
    {
        _embeddings = embeddings;
        _vectorStore = vectorStore;
        _qdrant = qdrant;
        _tenantContext = tenantContext;
        _log = log;
    }

    /// <summary>
    /// Synchronous ingestion for small text documents.
    /// For PDFs or large documents, use POST /documents/upload-pdf instead.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<IngestResponse>> Ingest([FromBody] IngestRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.DocumentId))
            return BadRequest("documentId is required");
        if (string.IsNullOrWhiteSpace(req.Text))
            return BadRequest("text is required");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var chunks = Chunker.Chunk(req.Text);

        var records = new List<VectorRecord>(chunks.Count);
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunkText = chunks[i];
            var embeddingResult = await _embeddings.EmbedAsync(chunkText, ct);
            
            // Track token usage for cost monitoring
            HttpContext.TrackTokenUsage(embeddingResult.TokenUsage);

            var payload = new Dictionary<string, object>
            {
                ["documentId"] = req.DocumentId,
                ["chunkIndex"] = i,
                ["text"] = chunkText
            };

            // Add tenant ID to payload for multi-tenant isolation
            if (_tenantContext.IsMultiTenantEnabled)
            {
                payload["tenantId"] = _tenantContext.RequiredTenantId;
            }

            records.Add(new VectorRecord(
                Id: StableUuid($"{_tenantContext.TenantId ?? "default"}:{req.DocumentId}:{i}"),
                Vector: embeddingResult.Embedding,
                Payload: payload
            ));
        }

        await _vectorStore.UpsertAsync(_qdrant.Collection, records, ct);
        sw.Stop();
        
        var tenantId = _tenantContext.TenantId ?? "default";
        _log.LogInformation("Ingested doc {DocId} chunks={Chunks} tenant={TenantId} ms={Ms}", 
            req.DocumentId, chunks.Count, tenantId, sw.ElapsedMilliseconds);
        
        return Ok(new IngestResponse(req.DocumentId, chunks.Count, tenantId));
    }

    static string StableUuid(string input)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash).ToString("D");
    }
}