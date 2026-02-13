using Microsoft.Extensions.Options;
using Rag.Core.Models;

namespace Rag.Api.Middleware;

/// <summary>
/// Simple header-based API key authentication middleware for internal access control.
/// </summary>
public sealed class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;
    private const string ApiKeyHeaderName = "X-API-Key";

    public ApiKeyAuthMiddleware(RequestDelegate next, ILogger<ApiKeyAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IOptions<SecuritySettings> securitySettings)
    {
        var settings = securitySettings.Value;

        // Skip auth for health/swagger/root endpoints
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (path == "/" || path.StartsWith("/swagger") || path.StartsWith("/health"))
        {
            await _next(context);
            return;
        }

        // If API key is not configured, skip validation (dev mode)
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            _logger.LogWarning("API key not configured - authentication disabled");
            await _next(context);
            return;
        }

        // Validate API key
        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
        {
            _logger.LogWarning("Missing API key from {RemoteIp}", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "unauthorized",
                message = $"API key required. Provide it via '{ApiKeyHeaderName}' header."
            });
            return;
        }

        if (!settings.ApiKey.Equals(extractedApiKey, StringComparison.Ordinal))
        {
            _logger.LogWarning("Invalid API key attempt from {RemoteIp}", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "unauthorized",
                message = "Invalid API key"
            });
            return;
        }

        await _next(context);
    }
}
