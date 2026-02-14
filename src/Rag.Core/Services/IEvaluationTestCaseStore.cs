using Rag.Core.Models;

namespace Rag.Core.Services;

/// <summary>
/// Storage interface for evaluation test cases
/// </summary>
public interface IEvaluationTestCaseStore
{
    /// <summary>
    /// Saves a test case
    /// </summary>
    Task SaveTestCaseAsync(EvaluationTestCase testCase, CancellationToken ct = default);
    
    /// <summary>
    /// Gets a test case by ID
    /// </summary>
    Task<EvaluationTestCase?> GetTestCaseAsync(string id, CancellationToken ct = default);
    
    /// <summary>
    /// Gets all test cases
    /// </summary>
    Task<List<EvaluationTestCase>> GetAllTestCasesAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Gets test cases by category
    /// </summary>
    Task<List<EvaluationTestCase>> GetTestCasesByCategoryAsync(string category, CancellationToken ct = default);
    
    /// <summary>
    /// Deletes a test case
    /// </summary>
    Task DeleteTestCaseAsync(string id, CancellationToken ct = default);
    
    /// <summary>
    /// Updates a test case
    /// </summary>
    Task UpdateTestCaseAsync(EvaluationTestCase testCase, CancellationToken ct = default);
}
