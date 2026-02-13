namespace Rag.Core.Models;

/// <summary>
/// Tracks token usage for a single API call.
/// Essential for cost monitoring, billing, and optimization.
/// </summary>
public sealed class TokenUsage
{
    public string Model { get; set; } = "";
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
}

/// <summary>
/// Aggregates cost tracking for an entire request (embedding + chat).
/// </summary>
public sealed class RequestCostSummary
{
    public int EmbeddingTokens { get; set; }
    public int ChatInputTokens { get; set; }
    public int ChatOutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal TotalCost { get; set; }
    public long DurationMs { get; set; }
}

/// <summary>
/// Model pricing information for cost calculation.
/// Prices are per 1M tokens unless otherwise specified.
/// </summary>
public sealed class ModelPricing
{
    public string Model { get; set; } = "";
    public string Provider { get; set; } = "";
    public decimal InputCostPer1MTokens { get; set; }
    public decimal OutputCostPer1MTokens { get; set; }
}

/// <summary>
/// Settings for cost tracking and monitoring.
/// </summary>
public sealed class CostTrackingSettings
{
    public bool Enabled { get; set; } = true;
    public bool LogCostSummary { get; set; } = true;
    public bool EmitMetrics { get; set; } = true;
    public decimal CostWarningThreshold { get; set; } = 0.10m; // 10 cents per request
}