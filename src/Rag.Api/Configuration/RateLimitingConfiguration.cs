using Microsoft.AspNetCore.RateLimiting;
using Rag.Core.Models;
using Rag.Core.Services;
using System.Threading.RateLimiting;

namespace Rag.Api.Configuration;

/// <summary>
/// Tier-based rate limiting configuration to prevent abuse and protect LLM costs.
/// Uses subscription tiers to apply different rate limits per user.
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

            // 💎 PHASE 3: Tier-based rate limiting for /ask endpoint
            // Uses user's subscription tier to determine rate limits
            options.AddPolicy(DefaultPolicy, context =>
            {
                var userContext = context.RequestServices.GetService<IUserContext>();
                var tierLimits = userContext?.TierLimits ?? TierLimits.Free;
                
                var userId = userContext?.UserId ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
                var partitionKey = $"ask:{userId}";

                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = tierLimits.AskRequestsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
            });

            // 💎 PHASE 3: Tier-based rate limiting for /ingest endpoint
            options.AddPolicy(IngestPolicy, context =>
            {
                var userContext = context.RequestServices.GetService<IUserContext>();
                var tierLimits = userContext?.TierLimits ?? TierLimits.Free;
                
                var userId = userContext?.UserId ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
                var partitionKey = $"ingest:{userId}";

                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = tierLimits.IngestRequestsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
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
                
                var userContext = context.HttpContext.RequestServices.GetService<IUserContext>();
                var tier = userContext?.Tier.ToString() ?? "Free";
                
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    error = "rate_limit_exceeded",
                    message = $"Rate limit exceeded for {tier} tier. Please try again later or upgrade your subscription.",
                    tier = tier,
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
