using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace Rag.Api.Configuration;

/// <summary>
/// Rate limiting configuration to prevent abuse and protect LLM costs.
/// </summary>
public static class RateLimitingConfiguration
{
    public const string DefaultPolicy = "default";
    public const string IngestPolicy = "ingest";

    public static IServiceCollection AddRagRateLimiting(this IServiceCollection services, IConfiguration config)
    {
        var settings = config.GetSection("RateLimiting").Get<RateLimitSettings>() ?? new RateLimitSettings();

        services.AddRateLimiter(options =>
        {
            // Reject requests when rate limit is exceeded
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Default policy: Fixed window for /ask endpoint
            options.AddFixedWindowLimiter(DefaultPolicy, opt =>
            {
                opt.PermitLimit = settings.AskRequestsPerMinute;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 0; // No queuing
            });

            // Ingest policy: More restrictive for expensive operations
            options.AddFixedWindowLimiter(IngestPolicy, opt =>
            {
                opt.PermitLimit = settings.IngestRequestsPerMinute;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 0;
            });

            // Global limiter: Prevent overwhelming the entire API
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                return RateLimitPartition.GetFixedWindowLimiter("global",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.GlobalRequestsPerMinute,
                        Window = TimeSpan.FromMinutes(1)
                    });
            });

            // Custom response for rate limit exceeded
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                
                double? retryAfterSeconds = null;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    retryAfterSeconds = retryAfter.TotalSeconds;
                }
                
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    error = "rate_limit_exceeded",
                    message = "Too many requests. Please try again later.",
                    retryAfter = retryAfterSeconds
                }, cancellationToken: cancellationToken);
            };
        });

        return services;
    }
}

public sealed class RateLimitSettings
{
    public int AskRequestsPerMinute { get; set; } = 30;
    public int IngestRequestsPerMinute { get; set; } = 10;
    public int GlobalRequestsPerMinute { get; set; } = 100;
}
