using FluentValidation;
using Hangfire;
using Hangfire.MemoryStorage;
using Rag.Api.Configuration;
using Rag.Api.Middleware;
using Rag.Core.Abstractions;
using Rag.Core.Agent;
using Rag.Core.Models;
using Rag.Core.Services;
using Rag.Infrastructure.Claude;
using Rag.Infrastructure.OpenAI;
using Rag.Infrastructure.Qdrant;
using Serilog;
using Serilog.Events;

// 📊 PHASE 7 - Observability: Configure Serilog for structured logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("Application", "RAG-API")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/rag-api-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
    )
    .CreateLogger();

try
{
    Log.Information("Starting RAG API application");

var builder = WebApplication.CreateBuilder(args);

// 📊 PHASE 7 - Observability: Use Serilog for logging
builder.Host.UseSerilog();

// Bind settings
builder.Services.Configure<QdrantSettings>(builder.Configuration.GetSection("Qdrant"));
builder.Services.Configure<OpenAiSettings>(builder.Configuration.GetSection("OpenAI"));
builder.Services.Configure<AnthropicSettings>(builder.Configuration.GetSection("Anthropic"));
builder.Services.Configure<SecuritySettings>(builder.Configuration.GetSection("Security"));
builder.Services.Configure<MultiTenancySettings>(builder.Configuration.GetSection("MultiTenancy"));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<CostTrackingSettings>(builder.Configuration.GetSection("CostTracking"));
builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection("Cors"));
builder.Services.Configure<ValidationSettings>(builder.Configuration.GetSection("Validation"));

// Register typed settings (simple injection)
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<QdrantSettings>>().Value);

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenAiSettings>>().Value);

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AnthropicSettings>>().Value);

// 🏢 PHASE 2 - Enterprise: Multi-Tenancy Support
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

// 🔐 PHASE 3 - Security & Cost: User Context & JWT Authentication
builder.Services.AddScoped<UserContext>();
builder.Services.AddScoped<IUserContext>(sp => sp.GetRequiredService<UserContext>());
builder.Services.AddSingleton<IJwtService, Rag.Infrastructure.Authentication.JwtService>();
builder.Services.AddSingleton<IAuthenticationService, Rag.Infrastructure.Authentication.InMemoryAuthenticationService>();

// 💰 PHASE 3 - Cost Tracking
builder.Services.AddSingleton<ICostCalculator, Rag.Infrastructure.Cost.CostCalculator>();

// 📂 PHASE 3 - Real-World Features: PDF & Background Jobs
builder.Services.AddSingleton<IPdfTextExtractor, Rag.Infrastructure.Pdf.PdfTextExtractor>();
builder.Services.AddSingleton<IDocumentIngestionService, Rag.Infrastructure.Services.DocumentIngestionService>();

// 🧪 PHASE 4 - Evaluation & Quality
builder.Services.AddSingleton<IEvaluationTestCaseStore, Rag.Infrastructure.Evaluation.JsonFileTestCaseStore>();
builder.Services.AddSingleton<Rag.Infrastructure.Evaluation.EvaluationRunStore>();
builder.Services.AddSingleton<IHallucinationDetector, Rag.Infrastructure.Evaluation.LlmHallucinationDetector>();
builder.Services.AddSingleton<IEvaluationService, Rag.Infrastructure.Evaluation.RagEvaluationService>();

// 🤖 PHASE 5 - Agent Layer: Tool-Calling Architecture
builder.Services.AddSingleton<IToolRegistry, Rag.Infrastructure.Agent.ToolRegistry>();
builder.Services.AddSingleton<IToolExecutor, Rag.Infrastructure.Agent.ToolExecutor>();
builder.Services.AddSingleton<IAgentOrchestrator, Rag.Infrastructure.Agent.AgentOrchestrator>();
builder.Services.AddSingleton<ICodebaseIngestionService, Rag.Infrastructure.Agent.CodebaseIngestionService>();

// Configure Hangfire for background job processing
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseMemoryStorage());

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 2; // Number of concurrent background workers
});

// ⚡ PHASE 1 - Hardening: Resilient HTTP Clients with Polly
builder.Services.AddResilientHttpClients(builder.Configuration);

// Register services with resilient HTTP clients
builder.Services.AddSingleton<OpenAiEmbeddingModel>();
builder.Services.AddSingleton<IEmbeddingModel>(sp =>
    new CachedEmbeddingModel(
        sp.GetRequiredService<OpenAiEmbeddingModel>()
    )
);  
builder.Services.AddSingleton<IChatModel, ClaudeChatModel>();
builder.Services.AddSingleton<IVectorStore, QdrantVectorStore>();

// ⚡ PHASE 1 - Hardening: Rate Limiting
builder.Services.AddRagRateLimiting(builder.Configuration);

// 🔐 PHASE 6 - Security Hardening: Configuration-driven CORS
var corsSettings = builder.Configuration.GetSection("Cors").Get<CorsSettings>() ?? new CorsSettings();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        if (corsSettings.AllowedOrigins.Count > 0)
        {
            policy.WithOrigins(corsSettings.AllowedOrigins.ToArray());
        }
        else
        {
            // Fallback to localhost for development
            policy.WithOrigins("http://localhost:3000", "http://localhost:3001", "http://localhost:3002");
        }

        policy.AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("X-Total-Cost", "X-Request-Id", "X-Correlation-Id");

        if (corsSettings.AllowCredentials)
        {
            policy.AllowCredentials();
        }

        policy.SetPreflightMaxAge(TimeSpan.FromSeconds(corsSettings.MaxAge));
    });
});

// 🔐 PHASE 6 - Security Hardening: FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Rag.Api.Validation.AskRequestValidator>();

// 📊 PHASE 7 - Observability: Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<Rag.Api.HealthChecks.QdrantHealthCheck>(
        "qdrant",
        tags: new[] { "database", "vector" })
    .AddCheck<Rag.Api.HealthChecks.ClaudeHealthCheck>(
        "claude",
        tags: new[] { "api", "llm" })
    .AddCheck<Rag.Api.HealthChecks.OpenAiHealthCheck>(
        "openai",
        tags: new[] { "api", "embeddings" });

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Register built-in tools after DI container is built
using (var scope = app.Services.CreateScope())
{
    var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
    var embeddingModel = scope.ServiceProvider.GetRequiredService<IEmbeddingModel>();
    var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();
    var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

    // RAG Search Tool
    var ragTool = new Rag.Infrastructure.Agent.Tools.RagSearchTool(embeddingModel, vectorStore);
    registry.RegisterTool(ragTool, new Rag.Core.Agent.ToolMetadata(
        ragTool.Name,
        ragTool.Description,
        Rag.Core.Agent.ToolCategory.RAG,
        new List<string> { "search", "rag", "documents", "retrieval" }
    ));

    // GitHub Search Repositories Tool
    var githubRepoTool = new Rag.Infrastructure.Agent.Tools.GitHubSearchRepositoriesTool(httpClientFactory);
    registry.RegisterTool(githubRepoTool, new Rag.Core.Agent.ToolMetadata(
        githubRepoTool.Name,
        githubRepoTool.Description,
        Rag.Core.Agent.ToolCategory.GitHub,
        new List<string> { "github", "repositories", "search" }
    ));

    // GitHub Search Code Tool
    var githubCodeTool = new Rag.Infrastructure.Agent.Tools.GitHubSearchCodeTool(httpClientFactory);
    registry.RegisterTool(githubCodeTool, new Rag.Core.Agent.ToolMetadata(
        githubCodeTool.Name,
        githubCodeTool.Description,
        Rag.Core.Agent.ToolCategory.GitHub,
        new List<string> { "github", "code", "search", "examples" }
    ));
}

// 📊 PHASE 7 - Observability: Global Exception Handling with RFC 7807 ProblemDetails
app.UseGlobalExceptionHandler();

// 🔐 PHASE 6 - Security Hardening: Security Headers
app.UseSecurityHeaders();

// 🔐 PHASE 6 - Security Hardening: Validation
app.UseValidation();

// CORS - Must be before authentication
app.UseCors("AllowFrontend");

// ⚡ PHASE 8 - Streaming: Server-Sent Events (SSE) Support
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/v1/ask/stream"))
    {
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Content-Type", "text/event-stream");
        context.Response.Headers.Append("Connection", "keep-alive");
        context.Response.Headers.Append("X-Accel-Buffering", "no"); // Disable nginx buffering
    }
    await next();
});

// PHASE 3 - Security: JWT Authentication (falls back to API key)
app.UseMiddleware<JwtAuthMiddleware>();

// PHASE 2 - Enterprise: Multi-Tenancy
app.UseMiddleware<TenantMiddleware>();

// PHASE 3 - Cost Tracking (tracks token usage and costs per request)
app.UseMiddleware<CostTrackingMiddleware>();

// PHASE 1 - Hardening: Rate Limiting
app.UseRateLimiter();

app.UseSwagger();
app.UseSwaggerUI();

// Hangfire Dashboard for monitoring background jobs
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [] // In production, add proper authorization
});

app.MapGet("/", () => "RAG API Running");

// 📊 PHASE 7 - Observability: Health Check Endpoints
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.ToString(),
            entries = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.ToString(),
                description = e.Value.Description,
                exception = e.Value.Exception?.Message
            })
        });
        await context.Response.WriteAsync(result);
    }
});

// Basic liveness probe (no dependency checks)
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false // No checks, just returns 200 OK if app is running
});

// Readiness probe (checks dependencies)
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("database") || check.Tags.Contains("vector")
});

app.MapControllers();

app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// Make Program accessible for integration testing
public partial class Program { }