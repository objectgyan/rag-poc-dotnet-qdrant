using Microsoft.AspNetCore.Mvc;
using Rag.Api.Models;
using Rag.Core.Agent;

namespace Rag.Api.Controllers;

/// <summary>
/// Agent controller for tool-calling and advanced AI capabilities.
/// </summary>
[ApiController]
[Route("[controller]")]
public class AgentController : ControllerBase
{
    private readonly IAgentOrchestrator _orchestrator;
    private readonly IToolRegistry _toolRegistry;
    private readonly ICodebaseIngestionService _codebaseService;

    public AgentController(
        IAgentOrchestrator orchestrator,
        IToolRegistry toolRegistry,
        ICodebaseIngestionService codebaseService)
    {
        _orchestrator = orchestrator;
        _toolRegistry = toolRegistry;
        _codebaseService = codebaseService;
    }

    /// <summary>
    /// Chat with the agent. The agent can use tools to answer questions.
    /// </summary>
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] AgentChatRequest request, CancellationToken cancellationToken)
    {
        var tenantId = HttpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();

        // Convert DTOs to domain models
        var history = request.ConversationHistory?.Select(m => new AgentMessage(
            m.Role,
            m.Content,
            m.ToolCall != null ? new ToolCall(m.ToolCall.ToolName, m.ToolCall.Arguments, m.ToolCall.ReasoningTrace) : null,
            m.ToolResult != null ? new ToolResult(m.ToolResult.Success, m.ToolResult.Content, m.ToolResult.Data, m.ToolResult.Error) : null
        )).ToList() ?? new List<AgentMessage>();

        var config = request.Config != null
            ? new AgentConfig(
                request.Config.MaxToolCalls,
                request.Config.AllowParallelToolCalls,
                request.Config.UseRagForContext,
                request.Config.TopKDocuments,
                request.Config.MinRelevanceScore,
                request.Config.EnableChainOfThought,
                request.Config.SystemPrompt
            )
            : new AgentConfig();

        // Process agent request
        var response = await _orchestrator.ProcessAsync(
            request.Message,
            history,
            config,
            tenantId,
            cancellationToken
        );

        // Convert back to DTOs
        var toolCallDtos = response.ToolCallsExecuted.Select(tc => new ToolCallDto(
            tc.ToolName,
            tc.Arguments,
            tc.ReasoningTrace
        )).ToList();

        var metricsDto = new AgentMetricsDto(
            response.Metrics.ToolCallsCount,
            response.Metrics.DocumentsRetrieved,
            response.Metrics.TotalDuration.TotalMilliseconds,
            response.Metrics.EstimatedCost,
            response.Metrics.ToolUsageCounts
        );

        return Ok(new AgentChatResponse(
            response.FinalAnswer,
            toolCallDtos,
            response.RetrievedDocuments,
            metricsDto
        ));
    }

    /// <summary>
    /// Get list of available tools.
    /// </summary>
    [HttpGet("tools")]
    public IActionResult GetTools()
    {
        var tools = _toolRegistry.GetAllTools();

        var toolDtos = tools.Select(tool =>
        {
            var metadata = _toolRegistry.GetToolMetadata(tool.Name);
            return new ToolInfoDto(
                tool.Name,
                tool.Description,
                metadata?.Category.ToString() ?? "Custom",
                tool.Parameters.Select(p => new ToolParameterDto(
                    p.Name,
                    p.Description,
                    p.Type,
                    p.Required,
                    p.DefaultValue
                )).ToList()
            );
        }).ToList();

        return Ok(toolDtos);
    }

    /// <summary>
    /// Get a specific tool by name.
    /// </summary>
    [HttpGet("tools/{toolName}")]
    public IActionResult GetTool(string toolName)
    {
        var tool = _toolRegistry.GetTool(toolName);
        if (tool == null)
        {
            return NotFound(new { error = $"Tool '{toolName}' not found" });
        }

        var metadata = _toolRegistry.GetToolMetadata(toolName);
        var toolDto = new ToolInfoDto(
            tool.Name,
            tool.Description,
            metadata?.Category.ToString() ?? "Custom",
            tool.Parameters.Select(p => new ToolParameterDto(
                p.Name,
                p.Description,
                p.Type,
                p.Required,
                p.DefaultValue
            )).ToList()
        );

        return Ok(toolDto);
    }

    /// <summary>
    /// Ingest a local codebase directory.
    /// </summary>
    [HttpPost("ingest-codebase")]
    public async Task<IActionResult> IngestCodebase([FromBody] IngestCodebaseRequest request, CancellationToken cancellationToken)
    {
        var tenantId = HttpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();

        var config = new CodebaseIngestionConfig(
            request.IncludePatterns,
            request.ExcludePatterns,
            ParseSemanticStructure: request.ParseSemanticStructure,
            ChunkSize: request.ChunkSize
        );

        var result = await _codebaseService.IngestDirectoryAsync(
            request.DirectoryPath,
            config,
            tenantId,
            cancellationToken
        );

        if (result.Error != null)
        {
            return BadRequest(new { error = result.Error });
        }

        var resultDto = new CodebaseIngestionResultDto(
            result.TotalFiles,
            result.TotalLines,
            result.ChunksCreated,
            result.FilesProcessed,
            result.ExtractedElements.Count,
            result.Duration.TotalSeconds
        );

        return Ok(resultDto);
    }

    /// <summary>
    /// Search for code snippets.
    /// </summary>
    [HttpPost("search-code")]
    public async Task<IActionResult> SearchCode([FromBody] CodeSearchRequest request, CancellationToken cancellationToken)
    {
        var tenantId = HttpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();

        var results = await _codebaseService.SearchCodeAsync(
            request.Query,
            tenantId,
            request.TopK,
            cancellationToken
        );

        var resultDtos = results.Select(r => new CodeSearchResultDto(
            r.FilePath,
            r.CodeSnippet,
            r.RelevanceScore
        )).ToList();

        return Ok(resultDtos);
    }

    /// <summary>
    /// Get code context for a specific file.
    /// </summary>
    [HttpGet("code-context")]
    public async Task<IActionResult> GetCodeContext(
        [FromQuery] string filePath,
        [FromQuery] int? startLine = null,
        [FromQuery] int? endLine = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = HttpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();

        var context = await _codebaseService.GetCodeContextAsync(
            filePath,
            startLine,
            endLine,
            tenantId,
            cancellationToken
        );

        if (context == null)
        {
            return NotFound(new { error = $"File not found: {filePath}" });
        }

        return Ok(new
        {
            file_path = context.FilePath,
            content = context.Content,
            start_line = context.StartLine,
            end_line = context.EndLine,
            language = context.FileLanguage,
            elements = context.RelatedElements.Select(e => new
            {
                name = e.Name,
                type = e.Type.ToString(),
                line = e.StartLine,
                signature = e.Signature
            }).ToList()
        });
    }
}
