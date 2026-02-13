using Rag.Core.Models;

namespace Rag.Core.Services;

/// <summary>
/// Scoped service holding current user's authentication and authorization context.
/// Populated by JWT middleware or API key middleware.
/// </summary>
public sealed class UserContext : IUserContext
{
    private string? _userId;
    private UserRole _role = UserRole.User;
    private SubscriptionTier _tier = SubscriptionTier.Free;

    public string? UserId => _userId;
    
    public UserRole Role => _role;
    
    public SubscriptionTier Tier => _tier;
    
    public TierLimits TierLimits => TierLimits.GetLimits(_tier);
    
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_userId);

    public bool HasRole(UserRole requiredRole) => _role >= requiredRole;

    public bool HasTier(SubscriptionTier requiredTier) => _tier >= requiredTier;

    /// <summary>
    /// Sets the user context. Called by authentication middleware.
    /// </summary>
    public void SetUser(string userId, UserRole role, SubscriptionTier tier)
    {
        _userId = userId;
        _role = role;
        _tier = tier;
    }
}
