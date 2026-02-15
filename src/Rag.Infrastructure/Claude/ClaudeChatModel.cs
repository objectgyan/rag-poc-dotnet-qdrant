using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
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

    /// <summary>
    /// Stream response token-by-token from Claude API (Phase 8).
    /// </summary>
    public async IAsyncEnumerable<string> StreamResponseAsync(
        string systemPrompt, 
        string userPrompt, 
        [EnumeratorCancellation] CancellationToken ct = default)
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
            },
            stream = true // Enable streaming
        });

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data: ")) continue;
            
            var data = line.Substring(6); // Remove "data: " prefix
            
            if (data == "[DONE]") break;

            // Parse JSON and extract token (without try-catch to allow yield)
            var token = ParseStreamingToken(data);
            if (!string.IsNullOrEmpty(token))
            {
                yield return token;
            }
        }
    }

    /// <summary>
    /// Parse streaming token from Claude API SSE event data.
    /// Returns null if parsing fails or no token is present.
    /// </summary>
    private static string? ParseStreamingToken(string jsonData)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonData);
            var root = doc.RootElement;
            
            // Claude streaming format: event types include message_start, content_block_delta, message_delta, message_stop
            if (root.TryGetProperty("type", out var typeElement))
            {
                var eventType = typeElement.GetString();
                
                if (eventType == "content_block_delta")
                {
                    if (root.TryGetProperty("delta", out var delta) && 
                        delta.TryGetProperty("text", out var text))
                    {
                        return text.GetString();
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Skip malformed JSON lines
        }
        
        return null;
    }
}