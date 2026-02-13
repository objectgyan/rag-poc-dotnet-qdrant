using Rag.Core.Models;

namespace Rag.Core.Abstractions;

public interface IVectorStore
{
    /// <summary>
    /// Upserts vector records into the collection.
    /// Records should include tenantId in payload for multi-tenant environments.
    /// </summary>
    Task UpsertAsync(string collection, IEnumerable<VectorRecord> records, CancellationToken ct);
    
    /// <summary>
    /// Searches for similar vectors in the collection.
    /// If tenantId is provided, filters results to only that tenant.
    /// </summary>
    Task<IReadOnlyList<VectorHit>> SearchAsync(
        string collection, 
        float[] queryVector, 
        int topK, 
        string? tenantId, 
        CancellationToken ct);
}