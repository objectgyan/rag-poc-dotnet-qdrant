using Rag.Core.Models;

namespace Rag.Core.Services;

/// <summary>
/// Provides access to the current authenticated user's context.
/// Includes role, tier, and authorization information.
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// Gets the current user's ID (from JWT sub claim or API key).
    /// </summary>
    string? UserId { get; }
    
    /// <summary>
    /// Gets the current user's role for RBAC.
    /// </summary>
    UserRole Role { get; }
    
    /// <summary>
    /// Gets the current user's subscription tier.
    /// </summary>
    SubscriptionTier Tier { get; }
    
    /// <summary>
    /// Gets the tier-specific limits for the current user.
    /// </summary>
    TierLimits TierLimits { get; }
    
    /// <summary>
    /// Checks if user is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }
    
    /// <summary>
    /// Checks if user has the specified role or higher.
    /// </summary>
    bool HasRole(UserRole requiredRole);
    
    /// <summary>
    /// Checks if user's tier meets or exceeds the required tier.
    /// </summary>
    bool HasTier(SubscriptionTier requiredTier);
}
