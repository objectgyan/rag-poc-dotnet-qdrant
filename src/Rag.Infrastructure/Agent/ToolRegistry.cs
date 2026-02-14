using Rag.Core.Agent;
using System.Collections.Concurrent;

namespace Rag.Infrastructure.Agent;

/// <summary>
/// Thread-safe registry for managing tools.
/// </summary>
public class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, ITool> _tools = new();
    private readonly ConcurrentDictionary<string, ToolMetadata> _metadata = new();

    public void RegisterTool(ITool tool, ToolMetadata? metadata = null)
    {
        _tools[tool.Name] = tool;

        if (metadata != null)
        {
            _metadata[tool.Name] = metadata;
        }
        else
        {
            // Create default metadata
            _metadata[tool.Name] = new ToolMetadata(
                tool.Name,
                tool.Description,
                ToolCategory.Custom,
                new List<string>()
            );
        }
    }

    public ITool? GetTool(string name)
    {
        _tools.TryGetValue(name, out var tool);
        return tool;
    }

    public IReadOnlyList<ITool> GetAllTools()
    {
        return _tools.Values.ToList();
    }

    public IReadOnlyList<ITool> GetToolsByCategory(ToolCategory category)
    {
        var toolsInCategory = new List<ITool>();

        foreach (var tool in _tools.Values)
        {
            if (_metadata.TryGetValue(tool.Name, out var metadata) && metadata.Category == category)
            {
                toolsInCategory.Add(tool);
            }
        }

        return toolsInCategory;
    }

    public IReadOnlyList<ITool> SearchTools(string query)
    {
        var lowerQuery = query.ToLowerInvariant();
        var matchingTools = new List<ITool>();

        foreach (var tool in _tools.Values)
        {
            // Search in name, description, and tags
            if (tool.Name.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
                tool.Description.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase))
            {
                matchingTools.Add(tool);
                continue;
            }

            // Check tags
            if (_metadata.TryGetValue(tool.Name, out var metadata))
            {
                if (metadata.Tags.Any(tag => tag.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase)))
                {
                    matchingTools.Add(tool);
                }
            }
        }

        return matchingTools;
    }

    public ToolMetadata? GetToolMetadata(string toolName)
    {
        _metadata.TryGetValue(toolName, out var metadata);
        return metadata;
    }

    public bool HasTool(string name)
    {
        return _tools.ContainsKey(name);
    }
}

/// <summary>
/// Executes tool calls with validation and error handling.
/// </summary>
public class ToolExecutor : IToolExecutor
{
    private readonly IToolRegistry _registry;

    public ToolExecutor(IToolRegistry registry)
    {
        _registry = registry;
    }

    public async Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken cancellationToken = default)
    {
        // Validate
        var (isValid, error) = ValidateToolCall(toolCall);
        if (!isValid)
        {
            return ToolResult.Fail(error!);
        }

        var tool = _registry.GetTool(toolCall.ToolName);
        if (tool == null)
        {
            return ToolResult.Fail($"Tool '{toolCall.ToolName}' not found");
        }

        try
        {
            return await tool.ExecuteAsync(toolCall.Arguments, cancellationToken);
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Tool execution failed: {ex.Message}");
        }
    }

    public async Task<Dictionary<string, ToolResult>> ExecuteParallelAsync(
        IEnumerable<ToolCall> toolCalls,
        CancellationToken cancellationToken = default)
    {
        var tasks = toolCalls.Select(async toolCall =>
        {
            var result = await ExecuteAsync(toolCall, cancellationToken);
            return (toolCall.ToolName, result);
        });

        var results = await Task.WhenAll(tasks);

        return results.ToDictionary(
            x => x.ToolName,
            x => x.result
        );
    }

    public (bool IsValid, string? Error) ValidateToolCall(ToolCall toolCall)
    {
        var tool = _registry.GetTool(toolCall.ToolName);
        if (tool == null)
        {
            return (false, $"Tool '{toolCall.ToolName}' does not exist");
        }

        // Check required parameters
        foreach (var param in tool.Parameters.Where(p => p.Required))
        {
            if (!toolCall.Arguments.ContainsKey(param.Name))
            {
                return (false, $"Required parameter '{param.Name}' is missing");
            }
        }

        // Type validation (basic)
        foreach (var arg in toolCall.Arguments)
        {
            var param = tool.Parameters.FirstOrDefault(p => p.Name == arg.Key);
            if (param == null)
            {
                return (false, $"Unknown parameter '{arg.Key}'");
            }

            // Basic type checking
            var argValue = arg.Value;
            var expectedType = param.Type.ToLowerInvariant();

            var isValidType = expectedType switch
            {
                "string" => argValue is string,
                "number" => argValue is int or long or double or float or decimal,
                "boolean" => argValue is bool,
                "array" => argValue is System.Collections.IEnumerable,
                "object" => argValue is Dictionary<string, object>,
                _ => true // Unknown type, skip validation
            };

            if (!isValidType)
            {
                return (false, $"Parameter '{arg.Key}' has invalid type. Expected {param.Type}");
            }
        }

        return (true, null);
    }
}
