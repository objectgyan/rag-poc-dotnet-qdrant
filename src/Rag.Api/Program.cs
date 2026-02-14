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

var builder = WebApplication.CreateBuilder(args);

// Bind settings
builder.Services.Configure<QdrantSettings>(builder.Configuration.GetSection("Qdrant"));
builder.Services.Configure<OpenAiSettings>(builder.Configuration.GetSection("OpenAI"));
builder.Services.Configure<AnthropicSettings>(builder.Configuration.GetSection("Anthropic"));
builder.Services.Configure<SecuritySettings>(builder.Configuration.GetSection("Security"));
builder.Services.Configure<MultiTenancySettings>(builder.Configuration.GetSection("MultiTenancy"));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<CostTrackingSettings>(builder.Configuration.GetSection("CostTracking"));

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

// CORS configuration for frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:3001", "http://localhost:3002")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("X-Total-Cost", "X-Request-Id");
    });
});

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

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var ex = feature?.Error;

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var payload = new
        {
            error = "internal_error",
            message = ex?.Message,
            traceId = context.TraceIdentifier
        };

        await context.Response.WriteAsJsonAsync(payload);
    });
});

// CORS - Must be before authentication
app.UseCors("AllowFrontend");

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
app.MapControllers();

app.Run();

// Make Program accessible for integration testing
public partial class Program { }