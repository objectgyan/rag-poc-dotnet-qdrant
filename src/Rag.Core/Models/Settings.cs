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

public sealed class MultiTenancySettings
{
    /// <summary>
    /// If true, X-Tenant-Id header is required for all requests.
    /// If false, uses DefaultTenantId when header is not provided.
    /// </summary>
    public bool RequireTenantId { get; set; } = false;
    
    /// <summary>
    /// Default tenant ID to use when multi-tenancy is not enforced and no header is provided.
    /// Leave empty for single-tenant mode.
    /// </summary>
    public string? DefaultTenantId { get; set; } = null;
    
    /// <summary>
    /// Enable multi-tenant features (tenant filtering, isolation).
    /// </summary>
    public bool Enabled { get; set; } = true;
}

public sealed class JwtSettings
{
    /// <summary>
    /// JWT issuer (e.g., "https://your-api.com").
    /// </summary>
    public string Issuer { get; set; } = "RagPocApi";
    
    /// <summary>
    /// JWT audience (e.g., "https://your-api.com").
    /// </summary>
    public string Audience { get; set; } = "RagPocClient";
    
    /// <summary>
    /// Secret key for signing JWTs. MUST be at least 32 characters.
    /// In production, use a secure random key and store in Azure Key Vault or secrets manager.
    /// </summary>
    public string SecretKey { get; set; } = "";
    
    /// <summary>
    /// Enable JWT authentication. If false, falls back to API key authentication.
    /// </summary>
    public bool Enabled { get; set; } = false;
}