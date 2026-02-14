using Rag.Core.Models;

namespace Rag.Core.Services;

/// <summary>
/// Service for evaluating RAG quality
/// </summary>
public interface IEvaluationService
{
    /// <summary>
    /// Evaluates a single test case
    /// </summary>
    Task<EvaluationResult> EvaluateTestCaseAsync(
        EvaluationTestCase testCase, 
        EvaluationConfig config,
        CancellationToken ct = default);
    
    /// <summary>
    /// Runs evaluation on all test cases
    /// </summary>
    Task<EvaluationRun> RunEvaluationAsync(
        string runName,
        EvaluationConfig config,
        string? category = null,
        CancellationToken ct = default);
    
    /// <summary>
    /// Gets evaluation run by ID
    /// </summary>
    Task<EvaluationRun?> GetEvaluationRunAsync(string runId, CancellationToken ct = default);
    
    /// <summary>
    /// Gets all evaluation runs
    /// </summary>
    Task<List<EvaluationRun>> GetAllEvaluationRunsAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Calculates aggregated metrics from results
    /// </summary>
    EvaluationMetrics CalculateMetrics(List<EvaluationResult> results);
}
