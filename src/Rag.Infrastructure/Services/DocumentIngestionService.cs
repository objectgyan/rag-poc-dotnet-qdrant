using Microsoft.Extensions.Logging;
using Rag.Core.Abstractions;
using Rag.Core.Models;
using Rag.Core.Services;
using Rag.Core.Text;

namespace Rag.Infrastructure.Services;

public sealed class DocumentIngestionService : IDocumentIngestionService
{
    private readonly IEmbeddingModel _embeddings;
    private readonly IVectorStore _vectorStore;
    private readonly IPdfTextExtractor _pdfExtractor;
    private readonly QdrantSettings _qdrant;
    private readonly ILogger<DocumentIngestionService> _logger;

    public DocumentIngestionService(
        IEmbeddingModel embeddings,
        IVectorStore vectorStore,
        IPdfTextExtractor pdfExtractor,
        QdrantSettings qdrant,
        ILogger<DocumentIngestionService> logger)
    {
        _embeddings = embeddings;
        _vectorStore = vectorStore;
        _pdfExtractor = pdfExtractor;
        _qdrant = qdrant;
        _logger = logger;
    }

    public async Task IngestTextAsync(string documentId, string text, string? tenantId, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        _logger.LogInformation("Starting text ingestion for document {DocumentId}, tenant {TenantId}", 
            documentId, tenantId ?? "default");

        var chunks = Chunker.Chunk(text);
        var records = new List<VectorRecord>(chunks.Count);

        for (int i = 0; i < chunks.Count; i++)
        {
            var embeddingResult = await _embeddings.EmbedAsync(chunks[i], ct);

            var payload = new Dictionary<string, object>
            {
                ["documentId"] = documentId,
                ["chunkIndex"] = i,
                ["text"] = chunks[i]
            };

            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                payload["tenantId"] = tenantId;
            }

            records.Add(new VectorRecord(
                Id: StableUuid($"{tenantId ?? "default"}:{documentId}:{i}"),
                Vector: embeddingResult.Embedding,
                Payload: payload
            ));
        }

        await _vectorStore.UpsertAsync(_qdrant.Collection, records, ct);
        
        sw.Stop();
        _logger.LogInformation("Completed text ingestion for document {DocumentId}, chunks={ChunkCount}, duration={DurationMs}ms",
            documentId, chunks.Count, sw.ElapsedMilliseconds);
    }

    public async Task IngestPdfAsync(string documentId, Stream pdfStream, string? tenantId, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        _logger.LogInformation("Starting PDF ingestion for document {DocumentId}, tenant {TenantId}", 
            documentId, tenantId ?? "default");

        // Extract text from PDF with page numbers
        var pages = await _pdfExtractor.ExtractTextAsync(pdfStream);
        
        _logger.LogInformation("Extracted {PageCount} pages from PDF {DocumentId}", pages.Count, documentId);

        // Chunk pages with metadata
        var chunks = Chunker.ChunkPdfPages(pages);
        var records = new List<VectorRecord>(chunks.Count);

        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var embeddingResult = await _embeddings.EmbedAsync(chunk.Text, ct);

            var payload = new Dictionary<string, object>
            {
                ["documentId"] = documentId,
                ["chunkIndex"] = i,
                ["text"] = chunk.Text
            };

            // Add page number if available
            if (chunk.PageNumber.HasValue)
            {
                payload["pageNumber"] = chunk.PageNumber.Value;
            }

            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                payload["tenantId"] = tenantId;
            }

            records.Add(new VectorRecord(
                Id: StableUuid($"{tenantId ?? "default"}:{documentId}:{i}"),
                Vector: embeddingResult.Embedding,
                Payload: payload
            ));
        }

        await _vectorStore.UpsertAsync(_qdrant.Collection, records, ct);
        
        sw.Stop();
        _logger.LogInformation("Completed PDF ingestion for document {DocumentId}, chunks={ChunkCount}, duration={DurationMs}ms",
            documentId, chunks.Count, sw.ElapsedMilliseconds);
    }

    public async Task DeleteDocumentAsync(string documentId, string? tenantId, CancellationToken ct)
    {
        _logger.LogInformation("Deleting document {DocumentId}, tenant {TenantId}", 
            documentId, tenantId ?? "default");

        await _vectorStore.DeleteByDocumentIdAsync(_qdrant.Collection, documentId, tenantId, ct);
        
        _logger.LogInformation("Deleted document {DocumentId}", documentId);
    }

    private static string StableUuid(string input)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash).ToString("D");
    }
}
