namespace Rag.Core.Models;

/// <summary>
/// User roles for role-based access control (RBAC).
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Read-only access - can only query, no ingestion.
    /// </summary>
    ReadOnly = 0,
    
    /// <summary>
    /// Standard user - can query and ingest documents.
    /// </summary>
    User = 1,
    
    /// <summary>
    /// Administrator - full access including management operations.
    /// </summary>
    Admin = 2
}

/// <summary>
/// Subscription tier for tier-based feature flags and rate limiting.
/// </summary>
public enum SubscriptionTier
{
    /// <summary>
    /// Free tier - limited features and rate limits.
    /// </summary>
    Free = 0,
    
    /// <summary>
    /// Professional tier - increased limits and features.
    /// </summary>
    Pro = 1,
    
    /// <summary>
    /// Enterprise tier - unlimited usage and premium features.
    /// </summary>
    Enterprise = 2
}

/// <summary>
/// Tier-specific configuration and limits.
/// </summary>
public sealed class TierLimits
{
    public SubscriptionTier Tier { get; init; }
    
    // Rate limits per minute
    public int AskRequestsPerMinute { get; init; }
    public int IngestRequestsPerMinute { get; init; }
    
    // Daily limits
    public int MaxRequestsPerDay { get; init; }
    public int MaxDocumentsPerDay { get; init; }
    
    // Feature flags
    public bool AllowIngest { get; init; }
    public bool AllowBulkIngest { get; init; }
    public bool AdvancedAnalytics { get; init; }
    public bool PrioritySupport { get; init; }
    
    // Cost limits
    public decimal MaxCostPerDay { get; init; }  // USD
    
    public static TierLimits Free => new()
    {
        Tier = SubscriptionTier.Free,
        AskRequestsPerMinute = 5,
        IngestRequestsPerMinute = 2,
        MaxRequestsPerDay = 100,
        MaxDocumentsPerDay = 10,
        AllowIngest = true,
        AllowBulkIngest = false,
        AdvancedAnalytics = false,
        PrioritySupport = false,
        MaxCostPerDay = 1.00m
    };
    
    public static TierLimits Pro => new()
    {
        Tier = SubscriptionTier.Pro,
        AskRequestsPerMinute = 50,
        IngestRequestsPerMinute = 20,
        MaxRequestsPerDay = 10000,
        MaxDocumentsPerDay = 1000,
        AllowIngest = true,
        AllowBulkIngest = true,
        AdvancedAnalytics = true,
        PrioritySupport = false,
        MaxCostPerDay = 50.00m
    };
    
    public static TierLimits Enterprise => new()
    {
        Tier = SubscriptionTier.Enterprise,
        AskRequestsPerMinute = int.MaxValue,
        IngestRequestsPerMinute = int.MaxValue,
        MaxRequestsPerDay = int.MaxValue,
        MaxDocumentsPerDay = int.MaxValue,
        AllowIngest = true,
        AllowBulkIngest = true,
        AdvancedAnalytics = true,
        PrioritySupport = true,
        MaxCostPerDay = decimal.MaxValue
    };
    
    public static TierLimits GetLimits(SubscriptionTier tier) => tier switch
    {
        SubscriptionTier.Free => Free,
        SubscriptionTier.Pro => Pro,
        SubscriptionTier.Enterprise => Enterprise,
        _ => Free
    };
}
