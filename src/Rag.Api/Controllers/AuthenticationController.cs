using Microsoft.AspNetCore.Mvc;
using Rag.Core.Models;
using Rag.Core.Services;

namespace Rag.Api.Controllers;

[ApiController]
[Route("api/v1/authentication")]
public sealed class AuthenticationController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthenticationController> _logger;

    public AuthenticationController(
        IAuthenticationService authService,
        IJwtService jwtService,
        ILogger<AuthenticationController> logger)
    {
        _authService = authService;
        _jwtService = jwtService;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user and returns a JWT token.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        _logger.LogInformation("Login attempt for username: {Username}", request.Username);

        var authResult = await _authService.AuthenticateAsync(request.Username, request.Password);
        
        if (authResult == null)
        {
            _logger.LogWarning("Login failed for username: {Username}", request.Username);
            return Unauthorized(new { error = "Invalid username or password" });
        }

        // Parse role and tier
        if (!Enum.TryParse<UserRole>(authResult.Role, out var role))
        {
            role = UserRole.User;
        }
        
        if (!Enum.TryParse<SubscriptionTier>(authResult.Tier, out var tier))
        {
            tier = SubscriptionTier.Free;
        }

        // Generate JWT token
        var token = _jwtService.GenerateToken(
            authResult.UserId, 
            authResult.TenantId, 
            role, 
            tier,
            expirationMinutes: 480 // 8 hours
        );

        _logger.LogInformation("Login successful for user {UserId} in tenant {TenantId}", 
            authResult.UserId, authResult.TenantId);

        return Ok(new LoginResponse
        {
            Token = token,
            UserId = authResult.UserId,
            Username = authResult.Username,
            TenantId = authResult.TenantId,
            TenantName = authResult.TenantName,
            Role = authResult.Role,
            Tier = authResult.Tier,
            ExpiresAt = DateTime.UtcNow.AddMinutes(480)
        });
    }

    /// <summary>
    /// Validates the current JWT token.
    /// </summary>
    [HttpGet("validate")]
    public IActionResult Validate()
    {
        // If we reach here, the JWT middleware has already validated the token
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return Unauthorized(new { error = "No token provided" });
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        var principal = _jwtService.ValidateToken(token);
        
        if (principal == null)
        {
            return Unauthorized(new { error = "Invalid token" });
        }

        var userId = _jwtService.GetUserId(principal);
        var tenantId = _jwtService.GetTenantId(principal);
        var role = _jwtService.GetRole(principal);
        var tier = _jwtService.GetTier(principal);

        return Ok(new TokenValidationResponse
        {
            Valid = true,
            UserId = userId ?? "unknown",
            TenantId = tenantId ?? "unknown",
            Role = role.ToString(),
            Tier = tier.ToString()
        });
    }
}

public sealed record LoginRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}

public sealed record LoginResponse
{
    public required string Token { get; init; }
    public required string UserId { get; init; }
    public required string Username { get; init; }
    public required string TenantId { get; init; }
    public required string TenantName { get; init; }
    public required string Role { get; init; }
    public required string Tier { get; init; }
    public required DateTime ExpiresAt { get; init; }
}

public sealed record TokenValidationResponse
{
    public required bool Valid { get; init; }
    public required string UserId { get; init; }
    public required string TenantId { get; init; }
    public required string Role { get; init; }
    public required string Tier { get; init; }
}
