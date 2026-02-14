using Microsoft.Extensions.Logging;
using Rag.Core.Abstractions;
using Rag.Core.Models;
using Rag.Core.Services;
using System.Diagnostics;

namespace Rag.Infrastructure.Evaluation;

/// <summary>
/// Main service for evaluating RAG quality
/// </summary>
public class RagEvaluationService : IEvaluationService
{
    private readonly IEvaluationTestCaseStore _testCaseStore;
    private readonly EvaluationRunStore _runStore;
    private readonly IChatModel _chatModel;
    private readonly IEmbeddingModel _embeddingModel;
    private readonly IVectorStore _vectorStore;
    private readonly IHallucinationDetector _hallucinationDetector;
    private readonly EmbeddingSimilarityEvaluator _similarityEvaluator;
    private readonly ILogger<RagEvaluationService> _logger;
    
    public RagEvaluationService(
        IEvaluationTestCaseStore testCaseStore,
        EvaluationRunStore runStore,
        IChatModel chatModel,
        IEmbeddingModel embeddingModel,
        IVectorStore vectorStore,
        IHallucinationDetector hallucinationDetector,
        ILogger<RagEvaluationService> logger)
    {
        _testCaseStore = testCaseStore;
        _runStore = runStore;
        _chatModel = chatModel;
        _embeddingModel = embeddingModel;
        _vectorStore = vectorStore;
        _hallucinationDetector = hallucinationDetector;
        _similarityEvaluator = new EmbeddingSimilarityEvaluator(embeddingModel);
        _logger = logger;
    }
    
    public async Task<EvaluationResult> EvaluateTestCaseAsync(
        EvaluationTestCase testCase, 
        EvaluationConfig config,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Evaluating test case: {TestCaseId}", testCase.Id);
        
        var sw = Stopwatch.StartNew();
        
        try
        {
            // 1. Generate answer using RAG pipeline
            var (actualAnswer, citations, context) = await GenerateAnswerAsync(testCase.Question, ct);
            
            sw.Stop();
            
            // 2. Compute semantic similarity
            double semanticScore = 0;
            if (config.UseSemanticEvaluation)
            {
                semanticScore = await _similarityEvaluator.ComputeSemanticSimilarityAsync(
                    testCase.ExpectedAnswer,
                    actualAnswer,
                    ct);
            }
            
            // 3. Compute keyword match
            double keywordScore = 0;
            List<string>? missingKeywords = null;
            if (config.UseKeywordEvaluation)
            {
                keywordScore = EmbeddingSimilarityEvaluator.ComputeKeywordMatchScore(
                    testCase.ExpectedAnswer,
                    actualAnswer,
                    testCase.RequiredKeywords);
                
                missingKeywords = FindMissingKeywords(testCase.RequiredKeywords, actualAnswer);
            }
            
            // 4. Evaluate citation accuracy
            var citationScore = EmbeddingSimilarityEvaluator.EvaluateCitationAccuracy(
                testCase.ExpectedDocumentId,
                citations?.Select(c => c.DocumentId).ToList());
            
            // 5. Detect hallucinations
            double hallucinationScore = 0;
            List<string> hallucinatedFacts = new();
            
            if (context.Count > 0)
            {
                (hallucinationScore, hallucinatedFacts) = await _hallucinationDetector.DetectHallucinationAsync(
                    actualAnswer,
                    context,
                    ct);
            }
            
            // 6. Compute overall score
            var overallScore = ComputeOverallScore(semanticScore, keywordScore, citationScore, hallucinationScore, config);
            
            // 7. Determine pass/fail
            var passed = semanticScore >= config.MinSemanticSimilarity &&
                        keywordScore >= config.MinKeywordMatch &&
                        citationScore >= config.MinCitationAccuracy &&
                        hallucinationScore <= config.MaxHallucinationRate;
            
            var failureReason = passed ? null : BuildFailureReason(
                semanticScore, keywordScore, citationScore, hallucinationScore, config);
            
            return new EvaluationResult
            {
                TestCaseId = testCase.Id,
                Question = testCase.Question,
                ExpectedAnswer = testCase.ExpectedAnswer,
                ActualAnswer = actualAnswer,
                Citations = citations,
                SemanticSimilarityScore = semanticScore,
                KeywordMatchScore = keywordScore,
                CitationAccuracyScore = citationScore,
                HallucinationScore = hallucinationScore,
                OverallScore = overallScore,
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                EstimatedCost = EstimateCost(actualAnswer, context),
                FailureReason = failureReason,
                MissingKeywords = missingKeywords,
                HallucinatedFacts = hallucinatedFacts,
                Passed = passed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating test case {TestCaseId}", testCase.Id);
            
            return new EvaluationResult
            {
                TestCaseId = testCase.Id,
                Question = testCase.Question,
                ExpectedAnswer = testCase.ExpectedAnswer,
                ActualAnswer = $"ERROR: {ex.Message}",
                SemanticSimilarityScore = 0,
                KeywordMatchScore = 0,
                CitationAccuracyScore = 0,
                HallucinationScore = 1.0,
                OverallScore = 0,
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                EstimatedCost = 0,
                FailureReason = $"Exception: {ex.Message}",
                Passed = false
            };
        }
    }
    
    public async Task<EvaluationRun> RunEvaluationAsync(
        string runName,
        EvaluationConfig config,
        string? category = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Starting evaluation run: {RunName}", runName);
        
        var runId = Guid.NewGuid().ToString("N");
        var startTime = DateTime.UtcNow;
        
        // Get test cases
        var testCases = category != null
            ? await _testCaseStore.GetTestCasesByCategoryAsync(category, ct)
            : await _testCaseStore.GetAllTestCasesAsync(ct);
        
        _logger.LogInformation("Running {Count} test cases", testCases.Count);
        
        // Evaluate each test case
        var results = new List<EvaluationResult>();
        foreach (var testCase in testCases)
        {
            var result = await EvaluateTestCaseAsync(testCase, config, ct);
            results.Add(result);
            
            _logger.LogInformation(
                "Test case {TestCaseId}: {Status} (Score: {Score:F2})",
                testCase.Id,
                result.Passed ? "PASSED" : "FAILED",
                result.OverallScore);
        }
        
        // Calculate metrics
        var metrics = CalculateMetrics(results);
        
        var run = new EvaluationRun
        {
            Id = runId,
            Name = runName,
            Description = category != null ? $"Category: {category}" : "All test cases",
            StartedAt = startTime,
            CompletedAt = DateTime.UtcNow,
            TotalTestCases = results.Count,
            PassedTestCases = results.Count(r => r.Passed),
            FailedTestCases = results.Count(r => !r.Passed),
            Metrics = metrics,
            Results = results
        };
        
        // Save run
        await _runStore.SaveRunAsync(run, ct);
        
        _logger.LogInformation(
            "Evaluation run completed: {Passed}/{Total} passed ({PassRate:F1}%)",
            run.PassedTestCases,
            run.TotalTestCases,
            metrics.PassRate * 100);
        
        return run;
    }
    
    public async Task<EvaluationRun?> GetEvaluationRunAsync(string runId, CancellationToken ct = default)
    {
        return await _runStore.GetRunAsync(runId, ct);
    }
    
    public async Task<List<EvaluationRun>> GetAllEvaluationRunsAsync(CancellationToken ct = default)
    {
        return await _runStore.GetAllRunsAsync(ct);
    }
    
    public EvaluationMetrics CalculateMetrics(List<EvaluationResult> results)
    {
        if (results.Count == 0)
        {
            return new EvaluationMetrics();
        }
        
        var passedCount = results.Count(r => r.Passed);
        
        return new EvaluationMetrics
        {
            AverageAccuracy = results.Average(r => r.OverallScore),
            AverageSemanticSimilarity = results.Average(r => r.SemanticSimilarityScore),
            AverageKeywordMatch = results.Average(r => r.KeywordMatchScore),
            AverageCitationAccuracy = results.Average(r => r.CitationAccuracyScore),
            HallucinationRate = results.Average(r => r.HallucinationScore),
            AverageResponseTimeMs = results.Average(r => r.ResponseTimeMs),
            AverageCostPerQuery = results.Average(r => r.EstimatedCost),
            TotalCost = results.Sum(r => r.EstimatedCost),
            TotalQueries = results.Count,
            PassedQueries = passedCount,
            FailedQueries = results.Count - passedCount,
            PassRate = (double)passedCount / results.Count
        };
    }
    
    private async Task<(string answer, List<EvaluationCitation>? citations, List<string> context)> GenerateAnswerAsync(
        string question,
        CancellationToken ct)
    {
        // Embed question
        var embeddingResult = await _embeddingModel.EmbedAsync(question, ct);
        
        // Search for similar chunks (assuming default collection name)
        var searchResults = await _vectorStore.SearchAsync("rag_collection", embeddingResult.Embedding, 3, null, ct);
        
        var citations = searchResults.Select(r => new EvaluationCitation
        {
            DocumentId = r.Payload.TryGetValue("documentId", out var docId) ? docId.ToString() : "",
            ChunkIndex = r.Payload.TryGetValue("chunkIndex", out var chunkIdx) ? Convert.ToInt32(chunkIdx) : 0,
            Score = r.Score,
            Text = r.Payload.TryGetValue("text", out var text) ? text.ToString() : "",
            PageNumber = r.Payload.TryGetValue("pageNumber", out var pageNum) ? Convert.ToInt32(pageNum) : null
        }).ToList();
        
        var context = searchResults
            .Select(r => r.Payload.TryGetValue("text", out var text) ? text.ToString() : "")
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();
        
        // Build prompt
        var (systemPrompt, userPrompt) = BuildRagPrompt(question, context);
        
        // Generate answer
        var result = await _chatModel.AnswerAsync(systemPrompt, userPrompt, ct);
        
        return (result.Answer, citations, context);
    }
    
    private static (string systemPrompt, string userPrompt) BuildRagPrompt(string question, List<string> context)
    {
        var contextStr = string.Join("\n\n", context.Select((c, i) => $"[{i + 1}] {c}"));
        
        var systemPrompt = "You are a helpful assistant that answers questions based on the provided context. Only use information from the context to answer.";
        
        var userPrompt = $@"Context:
{contextStr}

Question: {question}";
        
        return (systemPrompt, userPrompt);
    }
    
    private static double ComputeOverallScore(
        double semanticScore,
        double keywordScore,
        double citationScore,
        double hallucinationScore,
        EvaluationConfig config)
    {
        // Weighted average (adjust weights as needed)
        var weights = new Dictionary<string, double>
        {
            ["semantic"] = 0.4,
            ["keyword"] = 0.2,
            ["citation"] = 0.2,
            ["hallucination"] = 0.2
        };
        
        var score = 
            semanticScore * weights["semantic"] +
            keywordScore * weights["keyword"] +
            citationScore * weights["citation"] +
            (1.0 - hallucinationScore) * weights["hallucination"]; // Invert hallucination score
        
        return Math.Clamp(score, 0, 1);
    }
    
    private static string BuildFailureReason(
        double semanticScore,
        double keywordScore,
        double citationScore,
        double hallucinationScore,
        EvaluationConfig config)
    {
        var reasons = new List<string>();
        
        if (semanticScore < config.MinSemanticSimilarity)
        {
            reasons.Add($"Semantic similarity too low: {semanticScore:F2} < {config.MinSemanticSimilarity:F2}");
        }
        
        if (keywordScore < config.MinKeywordMatch)
        {
            reasons.Add($"Keyword match too low: {keywordScore:F2} < {config.MinKeywordMatch:F2}");
        }
        
        if (citationScore < config.MinCitationAccuracy)
        {
            reasons.Add($"Citation accuracy too low: {citationScore:F2} < {config.MinCitationAccuracy:F2}");
        }
        
        if (hallucinationScore > config.MaxHallucinationRate)
        {
            reasons.Add($"Hallucination rate too high: {hallucinationScore:F2} > {config.MaxHallucinationRate:F2}");
        }
        
        return string.Join("; ", reasons);
    }
    
    private static List<string>? FindMissingKeywords(List<string>? requiredKeywords, string actualAnswer)
    {
        if (requiredKeywords == null || requiredKeywords.Count == 0)
        {
            return null;
        }
        
        var actualLower = actualAnswer.ToLowerInvariant();
        return requiredKeywords
            .Where(kw => !actualLower.Contains(kw.ToLowerInvariant()))
            .ToList();
    }
    
    private static double EstimateCost(string answer, List<string> context)
    {
        // Rough estimate based on token count
        // Assumes OpenAI pricing: ~$0.002 per 1K tokens
        var totalChars = answer.Length + context.Sum(c => c.Length);
        var estimatedTokens = totalChars / 4.0; // Rough estimate: 1 token ≈ 4 chars
        return (estimatedTokens / 1000.0) * 0.002;
    }
}
