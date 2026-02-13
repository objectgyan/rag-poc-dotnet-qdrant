namespace Rag.Core.Services;

/// <summary>
/// Provides access to the current tenant context for the request.
/// Tenant ID is used to isolate data between different tenants in a multi-tenant environment.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Gets the current tenant ID for the request.
    /// Returns null if no tenant context is set (e.g., in single-tenant mode).
    /// </summary>
    string? TenantId { get; }
    
    /// <summary>
    /// Gets the current tenant ID, throwing if not set.
    /// Use this when tenant is required.
    /// </summary>
    string RequiredTenantId { get; }
    
    /// <summary>
    /// Checks if multi-tenancy is enabled for the current request.
    /// </summary>
    bool IsMultiTenantEnabled { get; }
}
