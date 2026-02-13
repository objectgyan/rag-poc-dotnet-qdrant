namespace Rag.Api.Models;

public sealed record IngestRequest(string DocumentId, string Text);

public sealed record IngestResponse(
    string DocumentId,
    int ChunkCount
);

public sealed record AskRequest(string Question, int TopK = 5);

public sealed record Citation(string DocumentId, int ChunkIndex, double Score);

public sealed record AskResponse(
    string Answer,
    List<Citation> Citations
);