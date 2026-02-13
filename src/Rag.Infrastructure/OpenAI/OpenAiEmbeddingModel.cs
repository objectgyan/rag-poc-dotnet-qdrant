using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Infrastructure.OpenAI;

public sealed class OpenAiEmbeddingModel : IEmbeddingModel
{
    private readonly HttpClient _http;
    private readonly OpenAiSettings _settings;

    public OpenAiEmbeddingModel(IHttpClientFactory httpClientFactory, OpenAiSettings settings)
    {
        // Use named client with resilience policies
        _http = httpClientFactory.CreateClient("OpenAiHttpClient");
        _settings = settings;
    }

    public async Task<EmbeddingResult> EmbedAsync(string text, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        req.Content = JsonContent.Create(new
        {
            model = _settings.EmbeddingModel,
            input = text
        });

        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var emb = doc.RootElement.GetProperty("data")[0].GetProperty("embedding");

        var vec = new float[emb.GetArrayLength()];
        int i = 0;
        foreach (var v in emb.EnumerateArray())
            vec[i++] = (float)v.GetDouble();

        // Parse token usage from response
        var usage = doc.RootElement.GetProperty("usage");
        var totalTokens = usage.GetProperty("total_tokens").GetInt32();
        
        var tokenUsage = new TokenUsage
        {
            Model = _settings.EmbeddingModel,
            InputTokens = totalTokens, // OpenAI embeddings only count input tokens
            OutputTokens = 0,
            TotalTokens = totalTokens
        };

        return new EmbeddingResult(vec, tokenUsage);
    }
}