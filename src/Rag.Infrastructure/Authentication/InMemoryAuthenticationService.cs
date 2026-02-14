using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Rag.Core.Services;

namespace Rag.Infrastructure.Authentication;

/// <summary>
/// In-memory authentication service with predefined users for demo purposes.
/// In production, this would connect to a database or identity provider.
/// </summary>
public sealed class InMemoryAuthenticationService : IAuthenticationService
{
    private readonly ILogger<InMemoryAuthenticationService> _logger;
    
    // Demo users: username -> (passwordHash, userId, tenantId, tenantName, role, tier)
    private readonly Dictionary<string, UserCredentials> _users = new()
    {
        ["admin@company.com"] = new("admin@company.com", HashPassword("admin123"), 
            "user-admin", "tenant-main", "Main Workspace", "Admin", "Enterprise"),
            
        ["mayank@company.com"] = new("mayank@company.com", HashPassword("mayank123"), 
            "user-mayank", "tenant-mayank", "Mayank's Workspace", "User", "Professional"),
            
        ["john@company.com"] = new("john@company.com", HashPassword("john123"), 
            "user-john", "tenant-john", "John's Workspace", "User", "Free"),
            
        ["sarah@company.com"] = new("sarah@company.com", HashPassword("sarah123"), 
            "user-sarah", "tenant-sarah", "Sarah's Workspace", "User", "Professional"),
    };

    public InMemoryAuthenticationService(ILogger<InMemoryAuthenticationService> logger)
    {
        _logger = logger;
    }

    public Task<AuthenticationResult?> AuthenticateAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return Task.FromResult<AuthenticationResult?>(null);
        }

        // Normalize username
        username = username.Trim().ToLowerInvariant();

        if (!_users.TryGetValue(username, out var credentials))
        {
            _logger.LogWarning("Authentication failed: User {Username} not found", username);
            return Task.FromResult<AuthenticationResult?>(null);
        }

        // Verify password
        var passwordHash = HashPassword(password);
        if (passwordHash != credentials.PasswordHash)
        {
            _logger.LogWarning("Authentication failed: Invalid password for user {Username}", username);
            return Task.FromResult<AuthenticationResult?>(null);
        }

        _logger.LogInformation("User {Username} authenticated successfully", username);

        return Task.FromResult<AuthenticationResult?>(new AuthenticationResult
        {
            UserId = credentials.UserId,
            Username = credentials.Username,
            TenantId = credentials.TenantId,
            TenantName = credentials.TenantName,
            Role = credentials.Role,
            Tier = credentials.Tier
        });
    }

    private static string HashPassword(string password)
    {
        // Simple SHA256 hash for demo purposes
        // In production, use BCrypt, Argon2, or PBKDF2 with salt
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private sealed record UserCredentials(
        string Username,
        string PasswordHash,
        string UserId,
        string TenantId,
        string TenantName,
        string Role,
        string Tier);
}
