using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Rag.Core.Abstractions;

namespace Rag.Infrastructure.OpenAI;

public sealed class CachedEmbeddingModel : IEmbeddingModel
{
    private readonly IEmbeddingModel _inner;
    private readonly ConcurrentDictionary<string, EmbeddingResult> _cache = new();

    public CachedEmbeddingModel(IEmbeddingModel inner)
    {
        _inner = inner;
    }

    public async Task<EmbeddingResult> EmbedAsync(string text, CancellationToken ct)
    {
        var key = Sha256(text);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var result = await _inner.EmbedAsync(text, ct);
        _cache[key] = result;
        return result;
    }

    private static string Sha256(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input ?? ""));
        return Convert.ToHexString(bytes);
    }
}