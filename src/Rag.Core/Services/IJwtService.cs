using System.Security.Claims;
using Rag.Core.Models;

namespace Rag.Core.Services;

/// <summary>
/// Service for JWT token generation and validation.
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Validates a JWT token and returns the claims principal.
    /// </summary>
    /// <param name="token">The JWT token to validate</param>
    /// <returns>ClaimsPrincipal if valid, null if invalid</returns>
    ClaimsPrincipal? ValidateToken(string token);
    
    /// <summary>
    /// Generates a JWT token for a user.
    /// Useful for testing and development scenarios.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="tenantId">Tenant identifier</param>
    /// <param name="role">User role</param>
    /// <param name="tier">Subscription tier</param>
    /// <param name="expirationMinutes">Token expiration in minutes (default 60)</param>
    /// <returns>JWT token string</returns>
    string GenerateToken(string userId, string? tenantId, UserRole role, SubscriptionTier tier, int expirationMinutes = 60);
    
    /// <summary>
    /// Extracts user ID from claims.
    /// </summary>
    string? GetUserId(ClaimsPrincipal principal);
    
    /// <summary>
    /// Extracts tenant ID from claims.
    /// </summary>
    string? GetTenantId(ClaimsPrincipal principal);
    
    /// <summary>
    /// Extracts user role from claims.
    /// </summary>
    UserRole GetRole(ClaimsPrincipal principal);
    
    /// <summary>
    /// Extracts subscription tier from claims.
    /// </summary>
    SubscriptionTier GetTier(ClaimsPrincipal principal);
}
