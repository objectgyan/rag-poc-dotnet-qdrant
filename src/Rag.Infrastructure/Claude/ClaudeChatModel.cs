using System.Net.Http.Json;
using System.Text.Json;
using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Infrastructure.Claude;

public sealed class ClaudeChatModel : IChatModel
{
    private readonly HttpClient _http;
    private readonly AnthropicSettings _settings;

    public ClaudeChatModel(IHttpClientFactory httpClientFactory, AnthropicSettings settings)
    {
        // Use named client with resilience policies
        _http = httpClientFactory.CreateClient("ClaudeHttpClient");
        _settings = settings;
    }

    public async Task<ChatResult> AnswerAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        req.Headers.Add("x-api-key", _settings.ApiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");

        req.Content = JsonContent.Create(new
        {
            model = _settings.Model,
            max_tokens = 800,
            system = systemPrompt,
            messages = new object[]
            {
                new { role = "user", content = userPrompt }
            }
        });

        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var answer = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
        
        // Parse token usage from response
        var usage = doc.RootElement.GetProperty("usage");
        var inputTokens = usage.GetProperty("input_tokens").GetInt32();
        var outputTokens = usage.GetProperty("output_tokens").GetInt32();
        
        var tokenUsage = new TokenUsage
        {
            Model = _settings.Model,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = inputTokens + outputTokens
        };

        return new ChatResult(answer, tokenUsage);
    }
}