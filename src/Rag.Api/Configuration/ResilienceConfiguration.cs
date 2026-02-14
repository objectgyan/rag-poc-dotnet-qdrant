using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Rag.Api.Configuration;

/// <summary>
/// Configures resilience policies for external service calls using Polly.
/// Handles transient failures, timeouts, and implements exponential backoff.
/// </summary>
public static class ResilienceConfiguration
{
    public const string ClaudeClient = "ClaudeHttpClient";
    public const string OpenAiClient = "OpenAiHttpClient";
    public const string QdrantClient = "QdrantHttpClient";

    /// <summary>
    /// Adds resilient HTTP clients for Claude, OpenAI, and Qdrant.
    /// </summary>
    public static IServiceCollection AddResilientHttpClients(this IServiceCollection services, IConfiguration config)
    {
        var settings = config.GetSection("Resilience").Get<ResilienceSettings>() ?? new ResilienceSettings();

        // Claude HTTP Client with resilience
        services.AddHttpClient(ClaudeClient)
            .AddStandardResilienceHandler(options =>
            {
                ConfigureRetryOptions(options.Retry, settings, "Claude");
                ConfigureTimeoutOptions(options.AttemptTimeout, settings);
                ConfigureTimeoutOptions(options.TotalRequestTimeout, settings);
                ConfigureCircuitBreakerOptions(options.CircuitBreaker, settings);
            });

        // OpenAI HTTP Client with resilience
        services.AddHttpClient(OpenAiClient)
            .AddStandardResilienceHandler(options =>
            {
                ConfigureRetryOptions(options.Retry, settings, "OpenAI");
                ConfigureTimeoutOptions(options.AttemptTimeout, settings);
                ConfigureTimeoutOptions(options.TotalRequestTimeout, settings);
                ConfigureCircuitBreakerOptions(options.CircuitBreaker, settings);
            });

        // Qdrant HTTP Client with resilience
        services.AddHttpClient(QdrantClient)
            .AddStandardResilienceHandler(options =>
            {
                ConfigureRetryOptions(options.Retry, settings, "Qdrant");
                ConfigureTimeoutOptions(options.AttemptTimeout, settings);
                ConfigureTimeoutOptions(options.TotalRequestTimeout, settings);
                ConfigureCircuitBreakerOptions(options.CircuitBreaker, settings);
            });

        return services;
    }

    private static void ConfigureRetryOptions(HttpRetryStrategyOptions retryOptions, ResilienceSettings settings, string serviceName)
    {
        retryOptions.MaxRetryAttempts = settings.MaxRetryAttempts;
        retryOptions.Delay = TimeSpan.FromMilliseconds(settings.InitialRetryDelayMs);
        retryOptions.BackoffType = DelayBackoffType.Exponential;
        retryOptions.UseJitter = true; // Add jitter to prevent thundering herd

        // Retry on transient HTTP errors and specific status codes
        retryOptions.ShouldHandle = args =>
        {
            var shouldRetry = args.Outcome switch
            {
                { Exception: HttpRequestException } => true,
                { Exception: TimeoutException } => true,
                { Result.StatusCode: System.Net.HttpStatusCode.RequestTimeout } => true,
                { Result.StatusCode: System.Net.HttpStatusCode.TooManyRequests } => true,
                { Result.StatusCode: >= System.Net.HttpStatusCode.InternalServerError } => true,
                _ => false
            };

            return ValueTask.FromResult(shouldRetry);
        };

        retryOptions.OnRetry = args =>
        {
            // Log retry attempts (logger can be accessed via dependency injection if configured)
            Console.WriteLine(
                $"[Resilience] {serviceName} request failed (Attempt {args.AttemptNumber}/{settings.MaxRetryAttempts}). " +
                $"Retrying after {args.RetryDelay.TotalMilliseconds}ms. " +
                $"Error: {args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString() ?? "Unknown"}"
            );

            return ValueTask.CompletedTask;
        };
    }

    private static void ConfigureTimeoutOptions(HttpTimeoutStrategyOptions timeoutOptions, ResilienceSettings settings)
    {
        timeoutOptions.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
    }

    private static void ConfigureCircuitBreakerOptions(HttpCircuitBreakerStrategyOptions circuitBreakerOptions, ResilienceSettings settings)
    {
        // Sampling duration must be at least double the attempt timeout
        circuitBreakerOptions.SamplingDuration = TimeSpan.FromSeconds(settings.TimeoutSeconds * 2);
        circuitBreakerOptions.FailureRatio = 0.5; // Open circuit if 50% of requests fail
        circuitBreakerOptions.MinimumThroughput = 3; // Minimum requests before circuit breaker activates
        circuitBreakerOptions.BreakDuration = TimeSpan.FromSeconds(30); // Stay open for 30 seconds
    }
}

public sealed class ResilienceSettings
{
    public int MaxRetryAttempts { get; set; } = 3;
    public int InitialRetryDelayMs { get; set; } = 500;
    public int TimeoutSeconds { get; set; } = 30;
}
