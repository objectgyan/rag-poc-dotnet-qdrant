namespace Rag.Core.Services;

/// <summary>
/// Service for detecting hallucinations in RAG responses
/// </summary>
public interface IHallucinationDetector
{
    /// <summary>
    /// Detects if the answer contains hallucinated information not present in the context
    /// </summary>
    /// <param name="answer">The generated answer</param>
    /// <param name="context">The retrieved context chunks</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Hallucination score (0.0 = no hallucination, 1.0 = completely hallucinated) and list of hallucinated facts</returns>
    Task<(double score, List<string> hallucinatedFacts)> DetectHallucinationAsync(
        string answer,
        List<string> context,
        CancellationToken ct = default);
}
