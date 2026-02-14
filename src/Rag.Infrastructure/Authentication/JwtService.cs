using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Rag.Core.Models;
using Rag.Core.Services;

namespace Rag.Infrastructure.Authentication;

public sealed class JwtService : IJwtService
{
    private readonly JwtSettings _settings;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly TokenValidationParameters _validationParameters;

    public JwtService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
        
        // Disable default claim type mapping (e.g., "sub" -> ClaimTypes.NameIdentifier)
        _tokenHandler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };
        
        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _settings.Issuer,
            ValidAudience = _settings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey)),
            ClockSkew = TimeSpan.FromMinutes(5) // Allow 5 minutes clock skew
        };
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var principal = _tokenHandler.ValidateToken(token, _validationParameters, out var validatedToken);
            
            // Additional validation: ensure it's a JWT token
            if (validatedToken is not JwtSecurityToken jwtToken ||
                !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }
            
            return principal;
        }
        catch
        {
            return null;
        }
    }

    public string GenerateToken(string userId, string? tenantId, UserRole role, SubscriptionTier tier, int expirationMinutes = 60)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("role", role.ToString()),
            new("tier", tier.ToString())
        };
        
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            claims.Add(new Claim("tenant_id", tenantId));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        return _tokenHandler.WriteToken(token);
    }

    public string? GetUserId(ClaimsPrincipal principal)
    {
        return principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    }

    public string? GetTenantId(ClaimsPrincipal principal)
    {
        return principal.FindFirst("tenant_id")?.Value;
    }

    public UserRole GetRole(ClaimsPrincipal principal)
    {
        var roleClaim = principal.FindFirst("role")?.Value;
        return Enum.TryParse<UserRole>(roleClaim, out var role) ? role : UserRole.User;
    }

    public SubscriptionTier GetTier(ClaimsPrincipal principal)
    {
        var tierClaim = principal.FindFirst("tier")?.Value;
        return Enum.TryParse<SubscriptionTier>(tierClaim, out var tier) ? tier : SubscriptionTier.Free;
    }
}
