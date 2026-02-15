using Microsoft.AspNetCore.Mvc;
using Rag.Core.Abstractions;
using Rag.Core.Models;
using Rag.Core.Services;

namespace Rag.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class MemoryController : ControllerBase
{
    private readonly IConversationMemory? _memory;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<MemoryController> _logger;

    public MemoryController(
        IConversationMemory? memory,
        ITenantContext tenantContext,
        ILogger<MemoryController> logger)
    {
        _memory = memory;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Store a new memory
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Store([FromBody] StoreMemoryRequest request, CancellationToken ct)
    {
        if (_memory == null)
        {
            return BadRequest(new { error = "Memory service is not enabled" });
        }

        try
        {
            var userId = request.UserId ?? "default-user";
            var tenantId = _tenantContext.TenantId ?? "default";

            var memoryId = await _memory.StoreAsync(
                request.Content,
                userId,
                tenantId,
                request.Type,
                request.Category ?? "",
                request.Importance,
                request.Metadata,
                ct);

            return Ok(new
            {
                memory_id = memoryId,
                user_id = userId,
                tenant_id = tenantId,
                content = request.Content,
                type = request.Type.ToString(),
                stored_at = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store memory");
            return StatusCode(500, new { error = "Failed to store memory", details = ex.Message });
        }
    }

    /// <summary>
    /// Search for relevant memories
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string query,
        [FromQuery] string? userId = null,
        [FromQuery] int topK = 10,
        [FromQuery] MemoryType? type = null,
        CancellationToken ct = default)
    {
        if (_memory == null)
        {
            return BadRequest(new { error = "Memory service is not enabled" });
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new { error = "Query is required" });
        }

        try
        {
            var uid = userId ?? "default-user";
            var tenantId = _tenantContext.TenantId ?? "default";

            var results = await _memory.SearchAsync(query, uid, tenantId, topK, type, ct);

            return Ok(new
            {
                query,
                user_id = uid,
                tenant_id = tenantId,
                results_count = results.Count,
                memories = results.Select(r => new
                {
                    memory_id = r.Memory.Id,
                    content = r.Memory.Content,
                    type = r.Memory.Type.ToString(),
                    category = r.Memory.Category,
                    importance = r.Memory.Importance,
                    relevance_score = r.RelevanceScore,
                    created_at = r.Memory.CreatedAt,
                    access_count = r.Memory.AccessCount,
                    metadata = r.Memory.Metadata
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search memories");
            return StatusCode(500, new { error = "Failed to search memories", details = ex.Message });
        }
    }

    /// <summary>
    /// Get all memories for a user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? userId = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        if (_memory == null)
        {
            return BadRequest(new { error = "Memory service is not enabled" });
        }

        try
        {
            var uid = userId ?? "default-user";
            var tenantId = _tenantContext.TenantId ?? "default";

            var memories = await _memory.GetAllAsync(uid, tenantId, skip, take, ct);

            return Ok(new
            {
                user_id = uid,
                tenant_id = tenantId,
                total_count = memories.Count,
                skip,
                take,
                memories = memories.Select(m => new
                {
                    memory_id = m.Id,
                    content = m.Content,
                    type = m.Type.ToString(),
                    category = m.Category,
                    importance = m.Importance,
                    created_at = m.CreatedAt,
                    last_accessed_at = m.LastAccessedAt,
                    access_count = m.AccessCount,
                    metadata = m.Metadata
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get memories");
            return StatusCode(500, new { error = "Failed to get memories", details = ex.Message });
        }
    }

    /// <summary>
    /// Get memory statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(
        [FromQuery] string? userId = null,
        CancellationToken ct = default)
    {
        if (_memory == null)
        {
            return BadRequest(new { error = "Memory service is not enabled" });
        }

        try
        {
            var uid = userId ?? "default-user";
            var tenantId = _tenantContext.TenantId ?? "default";

            var stats = await _memory.GetStatsAsync(uid, tenantId, ct);

            return Ok(new
            {
                user_id = uid,
                tenant_id = tenantId,
                total_count = stats.TotalCount,
                by_type = stats.CountByType.ToDictionary(
                    kvp => kvp.Key.ToString(),
                    kvp => kvp.Value),
                oldest_memory = stats.OldestMemory,
                newest_memory = stats.NewestMemory,
                total_accesses = stats.TotalAccessCount,
                average_importance = stats.AverageImportance
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get memory stats");
            return StatusCode(500, new { error = "Failed to get memory stats", details = ex.Message });
        }
    }

    /// <summary>
    /// Delete a specific memory
    /// </summary>
    [HttpDelete("{memoryId}")]
    public async Task<IActionResult> Delete(
        string memoryId,
        [FromQuery] string? userId = null,
        CancellationToken ct = default)
    {
        if (_memory == null)
        {
            return BadRequest(new { error = "Memory service is not enabled" });
        }

        try
        {
            var uid = userId ?? "default-user";
            var tenantId = _tenantContext.TenantId ?? "default";

            var success = await _memory.DeleteAsync(memoryId, uid, tenantId, ct);

            if (success)
            {
                return Ok(new { message = "Memory deleted successfully", memory_id = memoryId });
            }

            return NotFound(new { error = "Memory not found or could not be deleted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete memory");
            return StatusCode(500, new { error = "Failed to delete memory", details = ex.Message });
        }
    }

    /// <summary>
    /// Clear all memories for a user
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> Clear(
        [FromQuery] string? userId = null,
        CancellationToken ct = default)
    {
        if (_memory == null)
        {
            return BadRequest(new { error = "Memory service is not enabled" });
        }

        try
        {
            var uid = userId ?? "default-user";
            var tenantId = _tenantContext.TenantId ?? "default";

            var count = await _memory.ClearAsync(uid, tenantId, ct);

            return Ok(new
            {
                message = "Memories cleared successfully",
                user_id = uid,
                tenant_id = tenantId,
                cleared_count = count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear memories");
            return StatusCode(500, new { error = "Failed to clear memories", details = ex.Message });
        }
    }
}

/// <summary>
/// Request model for storing a memory
/// </summary>
public record StoreMemoryRequest
{
    public string Content { get; set; } = "";
    public string? UserId { get; set; }
    public MemoryType Type { get; set; } = MemoryType.Fact;
    public string? Category { get; set; }
    public int Importance { get; set; } = 5;
    public Dictionary<string, string>? Metadata { get; set; }
}
