using Rag.Core.Models;

namespace Rag.Core.Abstractions;

public interface IChatModel
{
    Task<ChatResult> AnswerAsync(string systemPrompt, string userPrompt, CancellationToken ct);
}

public record ChatResult(string Answer, TokenUsage TokenUsage);