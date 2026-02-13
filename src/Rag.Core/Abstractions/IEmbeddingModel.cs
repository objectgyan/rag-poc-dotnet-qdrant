namespace Rag.Core.Abstractions;

public interface IEmbeddingModel
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct);
}