using Rag.Core.Models;

namespace Rag.Core.Services;

/// <summary>
/// Service for calculating costs based on token usage and model pricing.
/// </summary>
public interface ICostCalculator
{
    /// <summary>
    /// Calculates the cost for token usage based on model pricing.
    /// </summary>
    /// <param name="tokenUsage">Token usage information</param>
    /// <returns>Estimated cost in USD</returns>
    decimal CalculateCost(TokenUsage tokenUsage);
    
    /// <summary>
    /// Gets the pricing for a specific model.
    /// </summary>
    /// <param name="model">Model name/identifier</param>
    /// <returns>Model pricing if found, null otherwise</returns>
    ModelPricing? GetPricing(string model);
}
