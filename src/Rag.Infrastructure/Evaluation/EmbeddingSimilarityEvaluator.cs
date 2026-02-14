using Rag.Core.Abstractions;

namespace Rag.Infrastructure.Evaluation;

/// <summary>
/// Evaluates answer quality using embedding similarity
/// </summary>
public class EmbeddingSimilarityEvaluator
{
    private readonly IEmbeddingModel _embeddingModel;
    
    public EmbeddingSimilarityEvaluator(IEmbeddingModel embeddingModel)
    {
        _embeddingModel = embeddingModel;
    }
    
    /// <summary>
    /// Computes cosine similarity between expected and actual answers
    /// </summary>
    public async Task<double> ComputeSemanticSimilarityAsync(
        string expectedAnswer, 
        string actualAnswer, 
        CancellationToken ct = default)
    {
        var expectedResult = await _embeddingModel.EmbedAsync(expectedAnswer, ct);
        var actualResult = await _embeddingModel.EmbedAsync(actualAnswer, ct);
        
        // Convert float[] to double[] for calculation
        var expectedEmbedding = Array.ConvertAll(expectedResult.Embedding, x => (double)x);
        var actualEmbedding = Array.ConvertAll(actualResult.Embedding, x => (double)x);
        
        return CosineSimilarity(expectedEmbedding, actualEmbedding);
    }
    
    /// <summary>
    /// Computes keyword match score
    /// </summary>
    public static double ComputeKeywordMatchScore(
        string expectedAnswer,
        string actualAnswer,
        List<string>? requiredKeywords = null)
    {
        // Normalize text
        var expectedLower = expectedAnswer.ToLowerInvariant();
        var actualLower = actualAnswer.ToLowerInvariant();
        
        // Extract keywords from expected answer if not provided
        var keywords = requiredKeywords ?? ExtractKeywords(expectedLower);
        
        if (keywords.Count == 0)
        {
            return 1.0; // No keywords to match
        }
        
        int matchedKeywords = keywords.Count(kw => actualLower.Contains(kw.ToLowerInvariant()));
        
        return (double)matchedKeywords / keywords.Count;
    }
    
    /// <summary>
    /// Evaluates citation accuracy
    /// </summary>
    public static double EvaluateCitationAccuracy(
        string? expectedDocumentId,
        List<string>? actualDocumentIds)
    {
        if (string.IsNullOrEmpty(expectedDocumentId))
        {
            return 1.0; // No expected document to check
        }
        
        if (actualDocumentIds == null || actualDocumentIds.Count == 0)
        {
            return 0.0; // Expected document but none retrieved
        }
        
        return actualDocumentIds.Contains(expectedDocumentId) ? 1.0 : 0.0;
    }
    
    private static double CosineSimilarity(double[] vec1, double[] vec2)
    {
        if (vec1.Length != vec2.Length)
        {
            throw new ArgumentException("Vectors must have same length");
        }
        
        double dotProduct = 0;
        double mag1 = 0;
        double mag2 = 0;
        
        for (int i = 0; i < vec1.Length; i++)
        {
            dotProduct += vec1[i] * vec2[i];
            mag1 += vec1[i] * vec1[i];
            mag2 += vec2[i] * vec2[i];
        }
        
        if (mag1 == 0 || mag2 == 0)
        {
            return 0;
        }
        
        return dotProduct / (Math.Sqrt(mag1) * Math.Sqrt(mag2));
    }
    
    private static List<string> ExtractKeywords(string text)
    {
        // Simple keyword extraction - split on whitespace and remove common words
        var stopWords = new HashSet<string> { "the", "a", "an", "is", "are", "was", "were", "in", "on", "at", "to", "for", "of", "and", "or", "but" };
        
        return text
            .Split(new[] { ' ', '\n', '\r', '\t', '.', ',', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length > 3 && !stopWords.Contains(word))
            .Distinct()
            .ToList();
    }
}
