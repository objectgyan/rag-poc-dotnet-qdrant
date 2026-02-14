namespace Rag.Core.Services;

/// <summary>
/// Service for authenticating users and managing credentials.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Authenticates a user with username and password.
    /// </summary>
    /// <returns>User details if authentication succeeds, null otherwise.</returns>
    Task<AuthenticationResult?> AuthenticateAsync(string username, string password);
}

/// <summary>
/// Result of authentication attempt.
/// </summary>
public sealed class AuthenticationResult
{
    public required string UserId { get; init; }
    public required string Username { get; init; }
    public required string TenantId { get; init; }
    public required string TenantName { get; init; }
    public required string Role { get; init; }
    public required string Tier { get; init; }
}
