using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Rag.Api.Configuration;
using Rag.Api.Models;
using Rag.Core.Services;

namespace Rag.Api.Controllers;

[ApiController]
[Route("documents")]
[EnableRateLimiting(RateLimitingConfiguration.IngestPolicy)]
public sealed class DocumentController : ControllerBase
{
    private readonly IDocumentIngestionService _ingestionService;
    private readonly ITenantContext _tenantContext;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<DocumentController> _logger;

    public DocumentController(
        IDocumentIngestionService ingestionService,
        ITenantContext tenantContext,
        IBackgroundJobClient backgroundJobs,
        ILogger<DocumentController> logger)
    {
        _ingestionService = ingestionService;
        _tenantContext = tenantContext;
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    /// <summary>
    /// Upload and ingest a PDF file in the background.
    /// Returns immediately with a job ID for tracking.
    /// </summary>
    [HttpPost("upload-pdf")]
    public async Task<ActionResult<IngestJobResponse>> UploadPdf(IFormFile file, [FromForm] string documentId)
    {
        if (file == null || file.Length == 0)
            return BadRequest("File is required");

        if (string.IsNullOrWhiteSpace(documentId))
            return BadRequest("documentId is required");

        if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only PDF files are supported");

        var tenantId = _tenantContext.TenantId;

        // Copy file stream to memory (Hangfire jobs need serializable data)
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        var pdfBytes = memoryStream.ToArray();

        // Queue background job
        var jobId = _backgroundJobs.Enqueue(() => 
            ProcessPdfIngestion(documentId, pdfBytes, tenantId, CancellationToken.None));

        _logger.LogInformation("Queued PDF ingestion job {JobId} for document {DocumentId}", jobId, documentId);

        return Ok(new IngestJobResponse(
            JobId: jobId,
            DocumentId: documentId,
            Status: "queued",
            Message: $"PDF ingestion job queued. Job ID: {jobId}"
        ));
    }

    /// <summary>
    /// Background job method for PDF ingestion.
    /// Called by Hangfire worker.
    /// </summary>
    [NonAction]
    public async Task ProcessPdfIngestion(string documentId, byte[] pdfBytes, string? tenantId, CancellationToken ct)
    {
        using var pdfStream = new MemoryStream(pdfBytes);
        await _ingestionService.IngestPdfAsync(documentId, pdfStream, tenantId, ct);
    }

    /// <summary>
    /// Delete a document and all its chunks.
    /// </summary>
    [HttpDelete("{documentId}")]
    public async Task<ActionResult<DeleteDocumentResponse>> DeleteDocument(string documentId)
    {
        if (string.IsNullOrWhiteSpace(documentId))
            return BadRequest("documentId is required");

        var tenantId = _tenantContext.TenantId;

        await _ingestionService.DeleteDocumentAsync(documentId, tenantId, CancellationToken.None);

        _logger.LogInformation("Deleted document {DocumentId} for tenant {TenantId}", 
            documentId, tenantId ?? "default");

        return Ok(new DeleteDocumentResponse(
            DocumentId: documentId,
            TenantId: tenantId ?? "default",
            Message: $"Document {documentId} deleted successfully"
        ));
    }

    /// <summary>
    /// Update a document by deleting old version and ingesting new one.
    /// For text documents, use background ingestion.
    /// </summary>
    [HttpPut("{documentId}")]
    public async Task<ActionResult<IngestJobResponse>> UpdateDocument(
        string documentId, 
        [FromBody] IngestRequest request)
    {
        if (string.IsNullOrWhiteSpace(documentId))
            return BadRequest("documentId is required");

        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest("text is required");

        var tenantId = _tenantContext.TenantId;

        // Queue background job for update (delete + ingest)
        var jobId = _backgroundJobs.Enqueue(() => 
            ProcessDocumentUpdate(documentId, request.Text, tenantId, CancellationToken.None));

        _logger.LogInformation("Queued document update job {JobId} for document {DocumentId}", jobId, documentId);

        return Ok(new IngestJobResponse(
            JobId: jobId,
            DocumentId: documentId,
            Status: "queued",
            Message: $"Document update job queued. Job ID: {jobId}"
        ));
    }

    /// <summary>
    /// Background job method for document update.
    /// </summary>
    [NonAction]
    public async Task ProcessDocumentUpdate(string documentId, string text, string? tenantId, CancellationToken ct)
    {
        // Delete old version
        await _ingestionService.DeleteDocumentAsync(documentId, tenantId, ct);
        
        // Ingest new version
        await _ingestionService.IngestTextAsync(documentId, text, tenantId, ct);
    }
}
