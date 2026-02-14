using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Rag.Api.Middleware;

/// <summary>
/// PHASE 7: Global exception handling middleware to catch unhandled exceptions
/// and return standardized RFC 7807 ProblemDetails responses.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var problemDetails = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "An error occurred while processing your request",
            Status = context.Response.StatusCode,
            Instance = context.Request.Path,
            Detail = _environment.IsDevelopment()
                ? exception.Message  // Show details in development
                : "An internal error occurred. Please contact support with the trace ID.", // Hide in production
        };

        // Add trace ID for support tracking
        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        // Add correlation ID if present
        if (context.Request.Headers.TryGetValue("X-Correlation-Id", out var correlationId))
        {
            problemDetails.Extensions["correlationId"] = correlationId.ToString();
        }

        // In development, include stack trace
        if (_environment.IsDevelopment() && exception.StackTrace != null)
        {
            problemDetails.Extensions["stackTrace"] = exception.StackTrace.Split(
                new[] { Environment.NewLine },
                StringSplitOptions.RemoveEmptyEntries
            );
        }

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        await context.Response.WriteAsync(json);
    }
}

/// <summary>
/// Extension method to register the Global Exception Middleware.
/// </summary>
public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
