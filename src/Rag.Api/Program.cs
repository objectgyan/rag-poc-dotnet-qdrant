using Rag.Api.Configuration;
using Rag.Api.Middleware;
using Rag.Core.Abstractions;
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

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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

// ⚡ PHASE 1 - Hardening: API Key Authentication
app.UseMiddleware<ApiKeyAuthMiddleware>();

// 🏢 PHASE 2 - Enterprise: Multi-Tenancy
app.UseMiddleware<TenantMiddleware>();

// ⚡ PHASE 1 - Hardening: Rate Limiting
app.UseRateLimiter();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => "RAG API Running");
app.MapControllers();

app.Run();