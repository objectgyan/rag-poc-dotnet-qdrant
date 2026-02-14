namespace Rag.Api.Middleware;

/// <summary>
/// Middleware to add security-related HTTP headers to all responses.
/// Protects against common web vulnerabilities (XSS, clickjacking, MIME sniffing).
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Strict-Transport-Security: Enforce HTTPS for 1 year, including subdomains
        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");

        // X-Content-Type-Options: Prevent MIME sniffing
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

        // X-Frame-Options: Prevent clickjacking by disabling framing
        context.Response.Headers.Append("X-Frame-Options", "DENY");

        // X-XSS-Protection: Enable browser XSS filter (legacy, but still useful)
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

        // Content-Security-Policy: Restrict resource loading
        context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'");

        // Referrer-Policy: Don't send referrer information
        context.Response.Headers.Append("Referrer-Policy", "no-referrer");

        // Permissions-Policy: Disable unnecessary browser features
        context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");

        // X-Permitted-Cross-Domain-Policies: Restrict cross-domain policies
        context.Response.Headers.Append("X-Permitted-Cross-Domain-Policies", "none");

        await _next(context);
    }
}

/// <summary>
/// Extension methods for SecurityHeadersMiddleware registration.
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
