namespace Rag.Api.Models;

/// <summary>
/// Request to ingest a document into the RAG system.
/// TenantId is automatically extracted from X-Tenant-Id header by middleware.
/// </summary>
public sealed record IngestRequest(string DocumentId, string Text);

public sealed record IngestResponse(
    string DocumentId,
    int ChunkCount,
    string TenantId
);

/// <summary>
/// Request to ask a question against the RAG system.
/// TenantId is automatically extracted from X-Tenant-Id header - ensures tenant isolation.
/// </summary>
public sealed record AskRequest(string Question, int TopK = 5);

public sealed record Citation(string DocumentId, int ChunkIndex, double Score);

public sealed record AskResponse(
    string Answer,
    List<Citation> Citations,
    string TenantId
);

/// <summary>
/// Response when starting a background ingestion job.
/// </summary>
public sealed record IngestJobResponse(
    string JobId,
    string DocumentId,
    string Status,
    string Message
);

/// <summary>
/// Response when deleting a document.
/// </summary>
public sealed record DeleteDocumentResponse(
    string DocumentId,
    string TenantId,
    string Message
);