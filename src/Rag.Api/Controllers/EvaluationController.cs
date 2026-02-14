using Microsoft.AspNetCore.Mvc;
using Rag.Api.Models;
using Rag.Core.Models;
using Rag.Core.Services;

namespace Rag.Api.Controllers;

[ApiController]
[Route("api/v1/evaluation")]
public class EvaluationController : ControllerBase
{
    private readonly IEvaluationTestCaseStore _testCaseStore;
    private readonly IEvaluationService _evaluationService;
    private readonly ILogger<EvaluationController> _logger;
    
    public EvaluationController(
        IEvaluationTestCaseStore testCaseStore,
        IEvaluationService evaluationService,
        ILogger<EvaluationController> logger)
    {
        _testCaseStore = testCaseStore;
        _evaluationService = evaluationService;
        _logger = logger;
    }
    
    /// <summary>
    /// Creates a new evaluation test case
    /// </summary>
    [HttpPost("test-cases")]
    public async Task<IActionResult> CreateTestCase([FromBody] CreateTestCaseRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Creating test case: {TestCaseId}", request.Id);
        
        var testCase = new EvaluationTestCase
        {
            Id = request.Id,
            Question = request.Question,
            ExpectedAnswer = request.ExpectedAnswer,
            ExpectedDocumentId = request.ExpectedDocumentId,
            RequiredKeywords = request.RequiredKeywords,
            Category = request.Category
        };
        
        await _testCaseStore.SaveTestCaseAsync(testCase, ct);
        
        return Ok(new { message = $"Test case {request.Id} created successfully", testCase });
    }
    
    /// <summary>
    /// Gets all test cases
    /// </summary>
    [HttpGet("test-cases")]
    public async Task<IActionResult> GetTestCases([FromQuery] string? category, CancellationToken ct)
    {
        var testCases = category != null
            ? await _testCaseStore.GetTestCasesByCategoryAsync(category, ct)
            : await _testCaseStore.GetAllTestCasesAsync(ct);
        
        return Ok(testCases);
    }
    
    /// <summary>
    /// Gets a specific test case
    /// </summary>
    [HttpGet("test-cases/{id}")]
    public async Task<IActionResult> GetTestCase(string id, CancellationToken ct)
    {
        var testCase = await _testCaseStore.GetTestCaseAsync(id, ct);
        
        if (testCase == null)
        {
            return NotFound(new { message = $"Test case {id} not found" });
        }
        
        return Ok(testCase);
    }
    
    /// <summary>
    /// Updates an existing test case
    /// </summary>
    [HttpPut("test-cases/{id}")]
    public async Task<IActionResult> UpdateTestCase(
        string id,
        [FromBody] UpdateTestCaseRequest request,
        CancellationToken ct)
    {
        var existingTestCase = await _testCaseStore.GetTestCaseAsync(id, ct);
        
        if (existingTestCase == null)
        {
            return NotFound(new { message = $"Test case {id} not found" });
        }
        
        var updatedTestCase = existingTestCase with
        {
            Question = request.Question,
            ExpectedAnswer = request.ExpectedAnswer,
            ExpectedDocumentId = request.ExpectedDocumentId,
            RequiredKeywords = request.RequiredKeywords,
            Category = request.Category
        };
        
        await _testCaseStore.UpdateTestCaseAsync(updatedTestCase, ct);
        
        return Ok(new { message = $"Test case {id} updated successfully", testCase = updatedTestCase });
    }
    
    /// <summary>
    /// Deletes a test case
    /// </summary>
    [HttpDelete("test-cases/{id}")]
    public async Task<IActionResult> DeleteTestCase(string id, CancellationToken ct)
    {
        await _testCaseStore.DeleteTestCaseAsync(id, ct);
        
        return Ok(new { message = $"Test case {id} deleted successfully" });
    }
    
    /// <summary>
    /// Runs evaluation on all test cases (or specific category)
    /// </summary>
    [HttpPost("run")]
    public async Task<IActionResult> RunEvaluation([FromBody] RunEvaluationRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Starting evaluation run: {RunName}", request.Name);
        
        // Build config with defaults
        var config = BuildEvaluationConfig(request.Config);
        
        // Run evaluation
        var run = await _evaluationService.RunEvaluationAsync(request.Name, config, request.Category, ct);
        
        var response = new EvaluationRunResponse
        {
            Id = run.Id,
            Name = run.Name,
            Status = "completed",
            TotalTestCases = run.TotalTestCases,
            PassedTestCases = run.PassedTestCases,
            FailedTestCases = run.FailedTestCases,
            PassRate = run.Metrics?.PassRate ?? 0,
            Message = $"Evaluation completed: {run.PassedTestCases}/{run.TotalTestCases} passed"
        };
        
        return Ok(response);
    }
    
    /// <summary>
    /// Gets evaluation run results
    /// </summary>
    [HttpGet("runs/{runId}")]
    public async Task<IActionResult> GetEvaluationRun(string runId, CancellationToken ct)
    {
        var run = await _evaluationService.GetEvaluationRunAsync(runId, ct);
        
        if (run == null)
        {
            return NotFound(new { message = $"Evaluation run {runId} not found" });
        }
        
        return Ok(run);
    }
    
    /// <summary>
    /// Gets all evaluation runs
    /// </summary>
    [HttpGet("runs")]
    public async Task<IActionResult> GetAllEvaluationRuns(CancellationToken ct)
    {
        var runs = await _evaluationService.GetAllEvaluationRunsAsync(ct);
        
        var summaries = runs.Select(r => new EvaluationSummaryResponse
        {
            RunId = r.Id,
            Name = r.Name,
            StartedAt = r.StartedAt,
            CompletedAt = r.CompletedAt,
            TotalTestCases = r.TotalTestCases,
            PassedTestCases = r.PassedTestCases,
            FailedTestCases = r.FailedTestCases,
            PassRate = r.Metrics?.PassRate ?? 0,
            AverageAccuracy = r.Metrics?.AverageAccuracy ?? 0,
            HallucinationRate = r.Metrics?.HallucinationRate ?? 0,
            AverageCostPerQuery = r.Metrics?.AverageCostPerQuery ?? 0,
            TotalCost = r.Metrics?.TotalCost ?? 0
        }).ToList();
        
        return Ok(summaries);
    }
    
    /// <summary>
    /// Gets aggregated metrics across all evaluation runs
    /// </summary>
    [HttpGet("metrics")]
    public async Task<IActionResult> GetAggregatedMetrics(CancellationToken ct)
    {
        var runs = await _evaluationService.GetAllEvaluationRunsAsync(ct);
        
        if (runs.Count == 0)
        {
            return Ok(new { message = "No evaluation runs found" });
        }
        
        var allResults = runs.SelectMany(r => r.Results ?? new List<EvaluationResult>()).ToList();
        var metrics = _evaluationService.CalculateMetrics(allResults);
        
        return Ok(new
        {
            totalRuns = runs.Count,
            totalQueries = metrics.TotalQueries,
            overallPassRate = metrics.PassRate,
            averageAccuracy = metrics.AverageAccuracy,
            hallucinationRate = metrics.HallucinationRate,
            averageResponseTimeMs = metrics.AverageResponseTimeMs,
            totalCost = metrics.TotalCost,
            metrics
        });
    }
    
    private static EvaluationConfig BuildEvaluationConfig(EvaluationConfigDto? dto)
    {
        var defaults = new EvaluationConfig();
        
        if (dto == null)
        {
            return defaults;
        }
        
        return new EvaluationConfig
        {
            MinSemanticSimilarity = dto.MinSemanticSimilarity ?? defaults.MinSemanticSimilarity,
            MinKeywordMatch = dto.MinKeywordMatch ?? defaults.MinKeywordMatch,
            MinCitationAccuracy = dto.MinCitationAccuracy ?? defaults.MinCitationAccuracy,
            MaxHallucinationRate = dto.MaxHallucinationRate ?? defaults.MaxHallucinationRate,
            UseSemanticEvaluation = dto.UseSemanticEvaluation ?? defaults.UseSemanticEvaluation,
            UseKeywordEvaluation = dto.UseKeywordEvaluation ?? defaults.UseKeywordEvaluation,
            UseLlmAsJudge = dto.UseLlmAsJudge ?? defaults.UseLlmAsJudge
        };
    }
}
