using Rag.Core.Models;

namespace Rag.Core.Abstractions;

public interface IChatModel
{
    Task<ChatResult> AnswerAsync(string systemPrompt, string userPrompt, CancellationToken ct);
    
    /// <summary>
    /// Stream chat response token-by-token for better UX (Phase 8).
    /// </summary>
    IAsyncEnumerable<string> StreamResponseAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}

public record ChatResult(string Answer, TokenUsage TokenUsage);