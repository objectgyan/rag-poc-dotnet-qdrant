namespace Rag.Core.Models;

public sealed class QdrantSettings
{
    public string Url { get; set; } = "http://localhost:6333";
    public string Collection { get; set; } = "rag_chunks";
}

public sealed class OpenAiSettings
{
    public string ApiKey { get; set; } = "";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
}

public sealed class AnthropicSettings
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "claude-3-5-sonnet-latest";
}

public sealed class SecuritySettings
{
    public string ApiKey { get; set; } = "";
}

public sealed class ResilienceSettings
{
    public int MaxRetryAttempts { get; set; } = 3;
    public int InitialRetryDelayMs { get; set; } = 500;
    public int TimeoutSeconds { get; set; } = 30;
}