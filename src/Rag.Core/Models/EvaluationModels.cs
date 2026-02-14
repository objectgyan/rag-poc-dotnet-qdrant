namespace Rag.Core.Models;

/// <summary>
/// Represents a test case for RAG evaluation
/// </summary>
public record EvaluationTestCase
{
    public required string Id { get; init; }
    public required string Question { get; init; }
    public required string ExpectedAnswer { get; init; }
    public string? ExpectedDocumentId { get; init; }
    public List<string>? RequiredKeywords { get; init; }
    public string? Category { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Represents the result of evaluating a single test case
/// </summary>
public record EvaluationResult
{
    public required string TestCaseId { get; init; }
    public required string Question { get; init; }
    public required string ExpectedAnswer { get; init; }
    public required string ActualAnswer { get; init; }
    public List<EvaluationCitation>? Citations { get; init; }
    
    // Scores (0.0 to 1.0)
    public double SemanticSimilarityScore { get; init; }
    public double KeywordMatchScore { get; init; }
    public double CitationAccuracyScore { get; init; }
    public double HallucinationScore { get; init; } // 0 = no hallucination, 1 = full hallucination
    public double OverallScore { get; init; }
    
    // Performance metrics
    public int ResponseTimeMs { get; init; }
    public double EstimatedCost { get; init; }
    
    // Detailed analysis
    public string? FailureReason { get; init; }
    public List<string>? MissingKeywords { get; init; }
    public List<string>? HallucinatedFacts { get; init; }
    public bool Passed { get; init; }
    
    public DateTime EvaluatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a batch evaluation run
/// </summary>
public record EvaluationRun
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int TotalTestCases { get; init; }
    public int PassedTestCases { get; init; }
    public int FailedTestCases { get; init; }
    public EvaluationMetrics? Metrics { get; init; }
    public List<EvaluationResult>? Results { get; init; }
}

/// <summary>
/// Aggregated evaluation metrics
/// </summary>
public record EvaluationMetrics
{
    // Overall metrics
    public double AverageAccuracy { get; init; }
    public double AverageSemanticSimilarity { get; init; }
    public double AverageKeywordMatch { get; init; }
    public double AverageCitationAccuracy { get; init; }
    public double HallucinationRate { get; init; }
    
    // Performance metrics
    public double AverageResponseTimeMs { get; init; }
    public double AverageCostPerQuery { get; init; }
    public double TotalCost { get; init; }
    
    // Pass/Fail metrics
    public int TotalQueries { get; init; }
    public int PassedQueries { get; init; }
    public int FailedQueries { get; init; }
    public double PassRate { get; init; }
    
    // Category breakdown (optional)
    public Dictionary<string, CategoryMetrics>? CategoryBreakdown { get; init; }
}

/// <summary>
/// Metrics for a specific category of test cases
/// </summary>
public record CategoryMetrics
{
    public required string Category { get; init; }
    public int TotalQueries { get; init; }
    public int PassedQueries { get; init; }
    public double PassRate { get; init; }
    public double AverageAccuracy { get; init; }
}

/// <summary>
/// Citation information from RAG response (for evaluation)
/// </summary>
public record EvaluationCitation
{
    public required string DocumentId { get; init; }
    public int ChunkIndex { get; init; }
    public double Score { get; init; }
    public string? Text { get; init; }
    public int? PageNumber { get; init; }
}

/// <summary>
/// Configuration for evaluation thresholds
/// </summary>
public record EvaluationConfig
{
    public double MinSemanticSimilarity { get; init; } = 0.7;
    public double MinKeywordMatch { get; init; } = 0.6;
    public double MinCitationAccuracy { get; init; } = 0.8;
    public double MaxHallucinationRate { get; init; } = 0.2;
    public bool UseSemanticEvaluation { get; init; } = true;
    public bool UseKeywordEvaluation { get; init; } = true;
    public bool UseLlmAsJudge { get; init; } = false; // Expensive but accurate
}
