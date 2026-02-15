namespace Rag.Core.Models;

/// <summary>
/// Represents a citation reference to a document chunk.
/// </summary>
public sealed record Citation(string DocumentId, int ChunkIndex, double Score);
