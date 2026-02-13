using Microsoft.AspNetCore.Mvc;
using Rag.Api.Models;
using Rag.Core.Abstractions;
using Rag.Core.Models;
using Rag.Core.Text;

namespace Rag.Api.Controllers;

[ApiController]
[Route("ingest")]
public sealed class IngestController : ControllerBase
{
    private readonly IEmbeddingModel _embeddings;
    private readonly IVectorStore _vectorStore;
    private readonly QdrantSettings _qdrant;

    public IngestController(IEmbeddingModel embeddings, IVectorStore vectorStore, QdrantSettings qdrant)
    {
        _embeddings = embeddings;
        _vectorStore = vectorStore;
        _qdrant = qdrant;
    }

    [HttpPost]
    public async Task<ActionResult<IngestResponse>> Ingest([FromBody] IngestRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.DocumentId))
            return BadRequest("documentId is required");
        if (string.IsNullOrWhiteSpace(req.Text))
            return BadRequest("text is required");

        var chunks = Chunker.Chunk(req.Text);

        var records = new List<VectorRecord>(chunks.Count);
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunkText = chunks[i];
            var vec = await _embeddings.EmbedAsync(chunkText, ct);

            records.Add(new VectorRecord(
                Id: Guid.NewGuid().ToString("D"),
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
        return Ok(new IngestResponse(req.DocumentId, chunks.Count));
    }
}