using Rag.Core.Models;
using Rag.Core.Services;
using System.Text.Json;

namespace Rag.Infrastructure.Evaluation;

/// <summary>
/// JSON file-based storage for evaluation test cases
/// Simple implementation for development - consider database for production
/// </summary>
public class JsonFileTestCaseStore : IEvaluationTestCaseStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    public JsonFileTestCaseStore(string? storagePath = null)
    {
        _filePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RagPoc",
            "evaluation-test-cases.json"
        );
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
    
    public async Task SaveTestCaseAsync(EvaluationTestCase testCase, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var testCases = await LoadAllAsync(ct);
            testCases.RemoveAll(tc => tc.Id == testCase.Id);
            testCases.Add(testCase);
            await SaveAllAsync(testCases, ct);
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task<EvaluationTestCase?> GetTestCaseAsync(string id, CancellationToken ct = default)
    {
        var testCases = await LoadAllAsync(ct);
        return testCases.FirstOrDefault(tc => tc.Id == id);
    }
    
    public async Task<List<EvaluationTestCase>> GetAllTestCasesAsync(CancellationToken ct = default)
    {
        return await LoadAllAsync(ct);
    }
    
    public async Task<List<EvaluationTestCase>> GetTestCasesByCategoryAsync(string category, CancellationToken ct = default)
    {
        var testCases = await LoadAllAsync(ct);
        return testCases.Where(tc => tc.Category == category).ToList();
    }
    
    public async Task DeleteTestCaseAsync(string id, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var testCases = await LoadAllAsync(ct);
            testCases.RemoveAll(tc => tc.Id == id);
            await SaveAllAsync(testCases, ct);
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task UpdateTestCaseAsync(EvaluationTestCase testCase, CancellationToken ct = default)
    {
        await SaveTestCaseAsync(testCase, ct);
    }
    
    private async Task<List<EvaluationTestCase>> LoadAllAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
        {
            return new List<EvaluationTestCase>();
        }
        
        var json = await File.ReadAllTextAsync(_filePath, ct);
        return JsonSerializer.Deserialize<List<EvaluationTestCase>>(json) ?? new List<EvaluationTestCase>();
    }
    
    private async Task SaveAllAsync(List<EvaluationTestCase> testCases, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(testCases, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(_filePath, json, ct);
    }
}
