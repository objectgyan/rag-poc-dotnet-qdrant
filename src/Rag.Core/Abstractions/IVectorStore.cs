using Rag.Core.Models;

namespace Rag.Core.Abstractions;

public interface IVectorStore
{
    Task UpsertAsync(string collection, IEnumerable<VectorRecord> records, CancellationToken ct);
    Task<IReadOnlyList<VectorHit>> SearchAsync(string collection, float[] queryVector, int topK, CancellationToken ct);
}