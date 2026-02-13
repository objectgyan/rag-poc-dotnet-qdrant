namespace Rag.Core.Services;

/// <summary>
/// Scoped service that holds the current tenant context for a request.
/// Populated by TenantMiddleware from X-Tenant-Id header.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private string? _tenantId;

    public string? TenantId => _tenantId;

    public string RequiredTenantId => _tenantId 
        ?? throw new InvalidOperationException("Tenant ID is required but not set. Ensure X-Tenant-Id header is provided.");

    public bool IsMultiTenantEnabled => !string.IsNullOrWhiteSpace(_tenantId);

    /// <summary>
    /// Sets the tenant ID for the current request. Called by middleware.
    /// </summary>
    public void SetTenantId(string? tenantId)
    {
        _tenantId = tenantId;
    }
}
