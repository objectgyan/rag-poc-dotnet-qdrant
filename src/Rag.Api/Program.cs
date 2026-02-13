using Rag.Core.Abstractions;
using Rag.Core.Models;
using Rag.Infrastructure.Claude;
using Rag.Infrastructure.OpenAI;
using Rag.Infrastructure.Qdrant;

var builder = WebApplication.CreateBuilder(args);

// Bind settings
builder.Services.Configure<QdrantSettings>(builder.Configuration.GetSection("Qdrant"));
builder.Services.Configure<OpenAiSettings>(builder.Configuration.GetSection("OpenAI"));
builder.Services.Configure<AnthropicSettings>(builder.Configuration.GetSection("Anthropic"));

// Register typed settings (simple injection)
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<QdrantSettings>>().Value);

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenAiSettings>>().Value);

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AnthropicSettings>>().Value);

// HttpClient
builder.Services.AddHttpClient();

// Register services
builder.Services.AddSingleton<IEmbeddingModel, OpenAiEmbeddingModel>();
builder.Services.AddSingleton<IChatModel, ClaudeChatModel>();
builder.Services.AddSingleton<IVectorStore, QdrantVectorStore>();
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => "RAG API Running");
app.MapControllers();

app.Run();