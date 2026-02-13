namespace Rag.Core.Abstractions;

public interface IChatModel
{
    Task<string> AnswerAsync(string systemPrompt, string userPrompt, CancellationToken ct);
}