using Microsoft.Extensions.Options;
using Rag.Core.Models;
using Rag.Core.Services;

namespace Rag.Api.Middleware;

/// <summary>
/// Extracts tenant ID from X-Tenant-Id header and sets it in TenantContext.
/// Enforces tenant requirement if multi-tenancy is enabled.
/// </summary>
public sealed class TenantMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMiddleware> _logger;
    private const string TenantIdHeaderName = "X-Tenant-Id";

    public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, IOptions<MultiTenancySettings> settings)
    {
        var multiTenancySettings = settings.Value;

        // Skip tenant extraction for health/swagger/root endpoints
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (path == "/" || path.StartsWith("/swagger") || path.StartsWith("/health"))
        {
            await _next(context);
            return;
        }

        // Extract tenant ID from header
        if (context.Request.Headers.TryGetValue(TenantIdHeaderName, out var tenantId))
        {
            var tenantIdValue = tenantId.ToString();
            
            // Validate tenant ID format (alphanumeric, hyphens, underscores only)
            if (!IsValidTenantId(tenantIdValue))
            {
                _logger.LogWarning("Invalid tenant ID format: {TenantId} from {RemoteIp}", 
                    tenantIdValue, context.Connection.RemoteIpAddress);
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "invalid_tenant_id",
                    message = "Tenant ID must contain only alphanumeric characters, hyphens, and underscores."
                });
                return;
            }

            tenantContext.SetTenantId(tenantIdValue);
            _logger.LogDebug("Tenant context set: {TenantId}", tenantIdValue);
        }
        else if (multiTenancySettings.RequireTenantId)
        {
            // Multi-tenancy is enforced, but no tenant ID provided
            _logger.LogWarning("Missing required tenant ID from {RemoteIp}", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "missing_tenant_id",
                message = $"Multi-tenancy is enabled. Provide tenant ID via '{TenantIdHeaderName}' header."
            });
            return;
        }
        else
        {
            // Single-tenant mode or optional multi-tenancy
            tenantContext.SetTenantId(multiTenancySettings.DefaultTenantId);
            if (!string.IsNullOrWhiteSpace(multiTenancySettings.DefaultTenantId))
            {
                _logger.LogDebug("Using default tenant: {TenantId}", multiTenancySettings.DefaultTenantId);
            }
        }

        await _next(context);
    }

    private static bool IsValidTenantId(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || tenantId.Length > 100)
            return false;

        // Allow only alphanumeric, hyphens, and underscores
        return tenantId.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
    }
}
