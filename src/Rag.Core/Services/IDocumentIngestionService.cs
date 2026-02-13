using Rag.Core.Models;

namespace Rag.Core.Services;

/// <summary>
/// Service for document ingestion operations.
/// </summary>
public interface IDocumentIngestionService
{
    /// <summary>
    /// Ingests a plain text document in the background.
    /// </summary>
    Task IngestTextAsync(string documentId, string text, string? tenantId, CancellationToken ct);
    
    /// <summary>
    /// Ingests a PDF document in the background.
    /// </summary>
    Task IngestPdfAsync(string documentId, Stream pdfStream, string? tenantId, CancellationToken ct);
    
    /// <summary>
    /// Deletes a document and all its chunks.
    /// </summary>
    Task DeleteDocumentAsync(string documentId, string? tenantId, CancellationToken ct);
}
