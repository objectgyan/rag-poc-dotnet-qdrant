using System.Net.Http.Json;
using System.Text.Json;
using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Infrastructure.Qdrant;

public sealed class QdrantVectorStore : IVectorStore
{
    private readonly HttpClient _http;
    private readonly QdrantSettings _settings;

    public QdrantVectorStore(IHttpClientFactory httpClientFactory, QdrantSettings settings)
    {
        // Use named client with resilience policies
        _http = httpClientFactory.CreateClient("QdrantHttpClient");
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

    public async Task<IReadOnlyList<VectorHit>> SearchAsync(
        string collection, 
        float[] queryVector, 
        int topK, 
        string? tenantId, 
        CancellationToken ct)
    {
        var url = $"{_settings.Url.TrimEnd('/')}/collections/{collection}/points/search";

        // Build search request with optional tenant filtering
        var searchRequest = new Dictionary<string, object>
        {
            ["vector"] = queryVector,
            ["limit"] = topK,
            ["with_payload"] = true
        };

        // Add tenant filter if multi-tenancy is enabled
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            searchRequest["filter"] = new
            {
                must = new[]
                {
                    new
                    {
                        key = "tenantId",
                        match = new { value = tenantId }
                    }
                }
            };
        }

        using var resp = await _http.PostAsJsonAsync(url, searchRequest, ct);

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

    public async Task DeleteByDocumentIdAsync(
        string collection, 
        string documentId, 
        string? tenantId, 
        CancellationToken ct)
    {
        var url = $"{_settings.Url.TrimEnd('/')}/collections/{collection}/points/delete?wait=true";

        // Build filter to delete all points with matching documentId (and optionally tenantId)
        var filter = new Dictionary<string, object>
        {
            ["must"] = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["key"] = "documentId",
                    ["match"] = new { value = documentId }
                }
            }
        };

        // Add tenant filter if provided
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            ((List<object>)filter["must"]).Add(new Dictionary<string, object>
            {
                ["key"] = "tenantId",
                ["match"] = new { value = tenantId }
            });
        }

        var deleteRequest = new { filter };

        using var resp = await _http.PostAsJsonAsync(url, deleteRequest, ct);

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Qdrant delete failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}"
            );
        }
    }
}