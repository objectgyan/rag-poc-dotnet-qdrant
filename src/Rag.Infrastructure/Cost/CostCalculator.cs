using Rag.Core.Models;
using Rag.Core.Services;

namespace Rag.Infrastructure.Cost;

public sealed class CostCalculator : ICostCalculator
{
    // Pricing database (per 1M tokens in USD)
    // Updated as of early 2025 - verify with current provider pricing
    private static readonly Dictionary<string, ModelPricing> _pricingDatabase = new(StringComparer.OrdinalIgnoreCase)
    {
        // OpenAI Embeddings
        ["text-embedding-3-small"] = new ModelPricing
        {
            Model = "text-embedding-3-small",
            Provider = "OpenAI",
            InputCostPer1MTokens = 0.02m,
            OutputCostPer1MTokens = 0m // Embeddings don't have output tokens
        },
        ["text-embedding-3-large"] = new ModelPricing
        {
            Model = "text-embedding-3-large",
            Provider = "OpenAI",
            InputCostPer1MTokens = 0.13m,
            OutputCostPer1MTokens = 0m
        },
        
        // OpenAI Chat Models
        ["gpt-4o"] = new ModelPricing
        {
            Model = "gpt-4o",
            Provider = "OpenAI",
            InputCostPer1MTokens = 2.50m,
            OutputCostPer1MTokens = 10.00m
        },
        ["gpt-4o-mini"] = new ModelPricing
        {
            Model = "gpt-4o-mini",
            Provider = "OpenAI",
            InputCostPer1MTokens = 0.15m,
            OutputCostPer1MTokens = 0.60m
        },
        
        // Anthropic Claude Models
        ["claude-3-5-sonnet-latest"] = new ModelPricing
        {
            Model = "claude-3-5-sonnet-latest",
            Provider = "Anthropic",
            InputCostPer1MTokens = 3.00m,
            OutputCostPer1MTokens = 15.00m
        },
        ["claude-sonnet-4-20250514"] = new ModelPricing
        {
            Model = "claude-sonnet-4-20250514",
            Provider = "Anthropic",
            InputCostPer1MTokens = 3.00m,
            OutputCostPer1MTokens = 15.00m
        },
        ["claude-3-5-haiku-latest"] = new ModelPricing
        {
            Model = "claude-3-5-haiku-latest",
            Provider = "Anthropic",
            InputCostPer1MTokens = 1.00m,
            OutputCostPer1MTokens = 5.00m
        },
        ["claude-3-opus-latest"] = new ModelPricing
        {
            Model = "claude-3-opus-latest",
            Provider = "Anthropic",
            InputCostPer1MTokens = 15.00m,
            OutputCostPer1MTokens = 75.00m
        }
    };

    public decimal CalculateCost(TokenUsage tokenUsage)
    {
        var pricing = GetPricing(tokenUsage.Model);
        if (pricing == null)
        {
            // Unknown model - use a conservative default estimate
            return (tokenUsage.InputTokens * 0.001m + tokenUsage.OutputTokens * 0.005m) / 1000m;
        }

        var inputCost = (tokenUsage.InputTokens / 1_000_000m) * pricing.InputCostPer1MTokens;
        var outputCost = (tokenUsage.OutputTokens / 1_000_000m) * pricing.OutputCostPer1MTokens;
        
        return inputCost + outputCost;
    }

    public ModelPricing? GetPricing(string model)
    {
        _pricingDatabase.TryGetValue(model, out var pricing);
        return pricing;
    }
}
