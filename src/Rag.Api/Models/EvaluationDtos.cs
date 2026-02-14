namespace Rag.Api.Models;

public record CreateTestCaseRequest
{
    public required string Id { get; init; }
    public required string Question { get; init; }
    public required string ExpectedAnswer { get; init; }
    public string? ExpectedDocumentId { get; init; }
    public List<string>? RequiredKeywords { get; init; }
    public string? Category { get; init; }
}

public record UpdateTestCaseRequest
{
    public required string Question { get; init; }
    public required string ExpectedAnswer { get; init; }
    public string? ExpectedDocumentId { get; init; }
    public List<string>? RequiredKeywords { get; init; }
    public string? Category { get; init; }
}

public record RunEvaluationRequest
{
    public required string Name { get; init; }
    public string? Category { get; init; }
    public EvaluationConfigDto? Config { get; init; }
}

public record EvaluationConfigDto
{
    public double? MinSemanticSimilarity { get; init; }
    public double? MinKeywordMatch { get; init; }
    public double? MinCitationAccuracy { get; init; }
    public double? MaxHallucinationRate { get; init; }
    public bool? UseSemanticEvaluation { get; init; }
    public bool? UseKeywordEvaluation { get; init; }
    public bool? UseLlmAsJudge { get; init; }
}

public record EvaluationRunResponse
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Status { get; init; }
    public int TotalTestCases { get; init; }
    public int PassedTestCases { get; init; }
    public int FailedTestCases { get; init; }
    public double PassRate { get; init; }
    public string? Message { get; init; }
}

public record EvaluationSummaryResponse
{
    public required string RunId { get; init; }
    public required string Name { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int TotalTestCases { get; init; }
    public int PassedTestCases { get; init; }
    public int FailedTestCases { get; init; }
    public double PassRate { get; init; }
    public double AverageAccuracy { get; init; }
    public double HallucinationRate { get; init; }
    public double AverageCostPerQuery { get; init; }
    public double TotalCost { get; init; }
}
