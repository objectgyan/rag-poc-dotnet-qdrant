using Rag.Core.Models;

namespace Rag.Core.Abstractions;

public interface IEmbeddingModel
{
    Task<EmbeddingResult> EmbedAsync(string text, CancellationToken ct);
}

public record EmbeddingResult(float[] Embedding, TokenUsage TokenUsage);