using System.Diagnostics;
using Microsoft.Extensions.Options;
using Rag.Core.Models;
using Rag.Core.Services;

namespace Rag.Api.Middleware;

/// <summary>
/// Middleware that tracks token usage and costs for each request.
/// Logs cost summaries and emits metrics for monitoring.
/// </summary>
public sealed class CostTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly CostTrackingSettings _settings;
    private readonly ILogger<CostTrackingMiddleware> _logger;

    public CostTrackingMiddleware(
        RequestDelegate next, 
        IOptions<CostTrackingSettings> settings,
        ILogger<CostTrackingMiddleware> logger)
    {
        _next = next;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context, 
        ICostCalculator costCalculator,
        IUserContext userContext,
        ITenantContext tenantContext)
    {
        if (!_settings.Enabled)
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        var costTracker = new RequestCostTracker(costCalculator);
        
        // Store cost tracker in HttpContext for controllers to use
        context.Items["CostTracker"] = costTracker;

        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            
            var summary = costTracker.GetSummary();
            summary.DurationMs = sw.ElapsedMilliseconds;

            if (summary.TotalCost > 0)
            {
                var userId = userContext.UserId ?? "anonymous";
                var tenantId = tenantContext.TenantId ?? "default";
                var tier = userContext.Tier.ToString();
                var endpoint = context.Request.Path;
                var statusCode = context.Response.StatusCode;

                // Log cost summary
                if (_settings.LogCostSummary)
                {
                    _logger.LogInformation(
                        "Cost Summary | User: {UserId} | Tenant: {TenantId} | Tier: {Tier} | Endpoint: {Endpoint} | " +
                        "Status: {StatusCode} | Duration: {DurationMs}ms | " +
                        "Embedding Tokens: {EmbeddingTokens} | Chat Input: {ChatInputTokens} | Chat Output: {ChatOutputTokens} | " +
                        "Total Tokens: {TotalTokens} | Cost: ${TotalCost:F6}",
                        userId, tenantId, tier, endpoint, statusCode, summary.DurationMs,
                        summary.EmbeddingTokens, summary.ChatInputTokens, summary.ChatOutputTokens,
                        summary.TotalTokens, summary.TotalCost);
                }

                // Emit metrics (can be extended to send to monitoring systems)
                if (_settings.EmitMetrics)
                {
                    // TODO: Integrate with metrics system (e.g., Prometheus, Application Insights)
                    // Example: _metrics.RecordTokenUsage(userId, tenantId, summary.TotalTokens);
                    // Example: _metrics.RecordCost(userId, tenantId, summary.TotalCost);
                }

                // Warn if cost exceeds threshold
                if (_settings.CostWarningThreshold > 0 && summary.TotalCost > _settings.CostWarningThreshold)
                {
                    _logger.LogWarning(
                        "High Cost Alert | User: {UserId} | Tenant: {TenantId} | Endpoint: {Endpoint} | " +
                        "Cost: ${TotalCost:F6} exceeds threshold ${Threshold:F6}",
                        userId, tenantId, endpoint, summary.TotalCost, _settings.CostWarningThreshold);
                }
            }
        }
    }
}

/// <summary>
/// Helper class to track costs across a single request.
/// Controllers add token usage, and this aggregates the total cost.
/// </summary>
public sealed class RequestCostTracker
{
    private readonly ICostCalculator _costCalculator;
    private readonly List<TokenUsage> _tokenUsages = new();

    public RequestCostTracker(ICostCalculator costCalculator)
    {
        _costCalculator = costCalculator;
    }

    /// <summary>
    /// Adds token usage to the request cost tracker.
    /// </summary>
    public void AddTokenUsage(TokenUsage tokenUsage)
    {
        _tokenUsages.Add(tokenUsage);
    }

    /// <summary>
    /// Gets the cost summary for this request.
    /// </summary>
    public RequestCostSummary GetSummary()
    {
        var embeddingTokens = 0;
        var chatInputTokens = 0;
        var chatOutputTokens = 0;
        var totalCost = 0m;

        foreach (var usage in _tokenUsages)
        {
            // Calculate cost for this usage
            var cost = _costCalculator.CalculateCost(usage);
            totalCost += cost;

            // Categorize tokens by model type
            var modelLower = usage.Model.ToLowerInvariant();
            if (modelLower.Contains("embedding"))
            {
                embeddingTokens += usage.TotalTokens;
            }
            else if (modelLower.Contains("gpt") || modelLower.Contains("claude"))
            {
                chatInputTokens += usage.InputTokens;
                chatOutputTokens += usage.OutputTokens;
            }
        }

        return new RequestCostSummary
        {
            EmbeddingTokens = embeddingTokens,
            ChatInputTokens = chatInputTokens,
            ChatOutputTokens = chatOutputTokens,
            TotalTokens = embeddingTokens + chatInputTokens + chatOutputTokens,
            TotalCost = totalCost
        };
    }
}

/// <summary>
/// Extension methods to easily access the cost tracker from controllers.
/// </summary>
public static class CostTrackingExtensions
{
    public static RequestCostTracker? GetCostTracker(this HttpContext context)
    {
        return context.Items.TryGetValue("CostTracker", out var tracker) 
            ? tracker as RequestCostTracker 
            : null;
    }

    public static void TrackTokenUsage(this HttpContext context, TokenUsage tokenUsage)
    {
        context.GetCostTracker()?.AddTokenUsage(tokenUsage);
    }
}
