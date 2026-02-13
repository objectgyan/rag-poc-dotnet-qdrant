using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Rag.Api.Configuration;
using Rag.Api.Models;
using Rag.Core.Abstractions;
using Rag.Core.Models;
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

    private readonly ILogger<IngestController> _log;

    public IngestController(IEmbeddingModel embeddings, IVectorStore vectorStore, QdrantSettings qdrant, ILogger<IngestController> log)
    {
        _embeddings = embeddings;
        _vectorStore = vectorStore;
        _qdrant = qdrant;
        _log = log;
    }

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
            var vec = await _embeddings.EmbedAsync(chunkText, ct);

            records.Add(new VectorRecord(
                Id: StableUuid($"{req.DocumentId}:{i}"),
                Vector: vec,
                Payload: new Dictionary<string, object>
                {
                    ["documentId"] = req.DocumentId,
                    ["chunkIndex"] = i,
                    ["text"] = chunkText
                }
            ));
        }

        await _vectorStore.UpsertAsync(_qdrant.Collection, records, ct);
        sw.Stop();
        _log.LogInformation("Ingested doc {DocId} chunks={Chunks} ms={Ms}", req.DocumentId, chunks.Count, sw.ElapsedMilliseconds);
        return Ok(new IngestResponse(req.DocumentId, chunks.Count));
    }

    static string StableUuid(string input)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash).ToString("D");
    }
}