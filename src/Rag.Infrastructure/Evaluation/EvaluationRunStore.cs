using Rag.Core.Models;
using System.Text.Json;

namespace Rag.Infrastructure.Evaluation;

/// <summary>
/// JSON file-based storage for evaluation runs
/// </summary>
public class EvaluationRunStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    public EvaluationRunStore(string? storagePath = null)
    {
        _filePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RagPoc",
            "evaluation-runs.json"
        );
        
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
    
    public async Task SaveRunAsync(EvaluationRun run, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var runs = await LoadAllAsync(ct);
            runs.RemoveAll(r => r.Id == run.Id);
            runs.Add(run);
            await SaveAllAsync(runs, ct);
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task<EvaluationRun?> GetRunAsync(string id, CancellationToken ct = default)
    {
        var runs = await LoadAllAsync(ct);
        return runs.FirstOrDefault(r => r.Id == id);
    }
    
    public async Task<List<EvaluationRun>> GetAllRunsAsync(CancellationToken ct = default)
    {
        return await LoadAllAsync(ct);
    }
    
    private async Task<List<EvaluationRun>> LoadAllAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
        {
            return new List<EvaluationRun>();
        }
        
        var json = await File.ReadAllTextAsync(_filePath, ct);
        return JsonSerializer.Deserialize<List<EvaluationRun>>(json) ?? new List<EvaluationRun>();
    }
    
    private async Task SaveAllAsync(List<EvaluationRun> runs, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(runs, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(_filePath, json, ct);
    }
}
