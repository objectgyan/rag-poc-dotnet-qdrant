using Microsoft.Extensions.Options;
using Rag.Core.Models;
using Rag.Core.Services;

namespace Rag.Api.Middleware;

/// <summary>
/// Middleware that validates JWT tokens and populates user and tenant context.
/// Falls back to API key authentication if JWT is disabled or token is not provided.
/// </summary>
public sealed class JwtAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly JwtSettings _jwtSettings;
    private readonly SecuritySettings _securitySettings;
    private readonly ILogger<JwtAuthMiddleware> _logger;

    public JwtAuthMiddleware(
        RequestDelegate next, 
        IOptions<JwtSettings> jwtSettings,
        IOptions<SecuritySettings> securitySettings,
        ILogger<JwtAuthMiddleware> logger)
    {
        _next = next;
        _jwtSettings = jwtSettings.Value;
        _securitySettings = securitySettings.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context, 
        IJwtService jwtService,
        IUserContext userContext,
        ITenantContext tenantContext)
    {
        // Skip authentication for certain paths
        var path = context.Request.Path.Value ?? "";
        if (path == "/" || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) || 
            path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        bool authenticated = false;

        // Try JWT authentication first if enabled
        if (_jwtSettings.Enabled)
        {
            authenticated = TryAuthenticateWithJwt(context, jwtService, userContext, tenantContext);
        }

        // Fall back to API key authentication if JWT auth failed or disabled
        if (!authenticated)
        {
            authenticated = TryAuthenticateWithApiKey(context, userContext);
        }

        if (!authenticated)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized. Provide a valid JWT token or API key." });
            return;
        }

        await _next(context);
    }

    private bool TryAuthenticateWithJwt(
        HttpContext context, 
        IJwtService jwtService,
        IUserContext userContext,
        ITenantContext tenantContext)
    {
        // Extract JWT from Authorization header
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        var principal = jwtService.ValidateToken(token);
        
        if (principal == null)
        {
            _logger.LogWarning("Invalid JWT token received");
            return false;
        }

        // Extract claims and populate context
        var userId = jwtService.GetUserId(principal);
        var tenantId = jwtService.GetTenantId(principal);
        var role = jwtService.GetRole(principal);
        var tier = jwtService.GetTier(principal);

        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("JWT token missing user ID (sub claim)");
            return false;
        }

        // Populate user context
        if (userContext is UserContext uc)
        {
            uc.SetUser(userId, role, tier);
        }

        // Populate tenant context from JWT
        if (!string.IsNullOrWhiteSpace(tenantId) && tenantContext is TenantContext tc)
        {
            tc.SetTenantId(tenantId);
        }

        _logger.LogDebug("JWT authentication successful for user {UserId}, tenant {TenantId}, role {Role}, tier {Tier}", 
            userId, tenantId ?? "none", role, tier);
        
        return true;
    }

    private bool TryAuthenticateWithApiKey(HttpContext context, IUserContext userContext)
    {
        // Extract API key from header
        var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
        
        // Allow bypass in development with empty key
        if (string.IsNullOrWhiteSpace(_securitySettings.ApiKey))
        {
            _logger.LogWarning("API key authentication is disabled (no key configured)");
            
            // Create anonymous user with default tier
            if (userContext is UserContext uc)
            {
                uc.SetUser("api-key-user", UserRole.User, SubscriptionTier.Free);
            }
            return true;
        }

        if (apiKey != _securitySettings.ApiKey)
        {
            return false;
        }

        // API key authenticated - use default user with User role and Free tier
        if (userContext is UserContext uc2)
        {
            uc2.SetUser("api-key-user", UserRole.User, SubscriptionTier.Free);
        }

        _logger.LogDebug("API key authentication successful");
        return true;
    }
}
