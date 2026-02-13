using System.Net.Http.Json;
using System.Text.Json;
using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Infrastructure.Qdrant;

public sealed class QdrantVectorStore : IVectorStore
{
    private readonly HttpClient _http;
    private readonly QdrantSettings _settings;

    public QdrantVectorStore(HttpClient http, QdrantSettings settings)
    {
        _http = http;
        _settings = settings;
    }

    public async Task UpsertAsync(string collection, IEnumerable<VectorRecord> records, CancellationToken ct)
    {
        var url = $"{_settings.Url.TrimEnd('/')}/collections/{collection}/points?wait=true";
        var points = records.Select(r => new { id = r.Id, vector = r.Vector, payload = r.Payload }).ToArray();

        using var resp = await _http.PutAsJsonAsync(url, new { points }, ct);

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Qdrant upsert failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}"
            );
        }
    }

    public async Task<IReadOnlyList<VectorHit>> SearchAsync(string collection, float[] queryVector, int topK, CancellationToken ct)
    {
        var url = $"{_settings.Url.TrimEnd('/')}/collections/{collection}/points/search";

        using var resp = await _http.PostAsJsonAsync(url, new
        {
            vector = queryVector,
            limit = topK,
            with_payload = true
        }, ct);

        resp.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var result = doc.RootElement.GetProperty("result");

        var hits = new List<VectorHit>();
        foreach (var item in result.EnumerateArray())
        {
            var id = item.GetProperty("id").ToString();
            var score = item.GetProperty("score").GetDouble();
            var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(item.GetProperty("payload").GetRawText())!;
            hits.Add(new VectorHit(id, score, payload));
        }

        return hits;
    }
}