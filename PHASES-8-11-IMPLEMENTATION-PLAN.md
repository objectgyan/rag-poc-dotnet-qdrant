# 🚀 Phases 8-11 Implementation Plan

**Status**: In Progress  
**Last Updated**: February 14, 2026

---

## 📊 Current State Assessment

### Phase 8 - Streaming (Partially Complete ~60%)

#### ✅ Completed
- Claude streaming support (`IAsyncEnumerable<string> StreamResponseAsync`)
- SSE endpoint `/api/v1/ask/stream` with proper headers
- Frontend `useSSE` hook with authentication headers
- Frontend `StreamingChat` component with real-time UI updates
- Agent orchestrator `StreamAsync` method

#### ❌ Missing
- Agent streaming endpoint in AgentController
- WebSocket support for bidirectional communication
- OpenAI streaming support (only Claude implemented)
- Integration tests for streaming endpoints
- Streaming documentation completion

---

## 🎯 Phase 8 - Streaming & Real-time Communication (Completion)

### Priority: HIGH | Estimated Time: 4-6 hours

### Tasks

#### 1. Add Agent Streaming Endpoint
**File**: `src/Rag.Api/Controllers/AgentController.cs`
- [ ] Create `POST /api/v1/agent/chat/stream` endpoint
- [ ] Use SSE format with different chunk types (reasoning, tool_call, content)
- [ ] Handle authentication and tenant isolation
- [ ] Add proper cancellation token support

#### 2. WebSocket Support
**Files**: 
- `src/Rag.Api/Hubs/ChatHub.cs` (new)
- `src/Rag.Api/Program.cs` (update)
- `src/Rag.Web/src/hooks/useWebSocket.ts` (new)

- [ ] Create SignalR ChatHub with streaming methods
- [ ] Add WebSocket endpoint configuration in Program.cs
- [ ] Implement tenant-aware WebSocket connections
- [ ] Create React hook for WebSocket client
- [ ] Add connection state management
- [ ] Handle reconnection logic

#### 3. OpenAI Streaming Support
**File**: `src/Rag.Infrastructure/OpenAI/OpenAiEmbeddingModel.cs` (or new chat model)
- [ ] Implement `IAsyncEnumerable<string> StreamResponseAsync` for OpenAI
- [ ] Use OpenAI SDK streaming API
- [ ] Add configuration option to choose between OpenAI/Claude for chat

#### 4. Integration Tests
**File**: `src/Rag.Tests/StreamingTests.cs` (new)
- [ ] Test SSE endpoint for /ask/stream
- [ ] Test agent streaming endpoint
- [ ] Test error handling and cancellation
- [ ] Test WebSocket connections

### Deliverables
- Working agent streaming with tool call visualization
- WebSocket bidirectional chat
- Complete streaming documentation
- Integration tests with 80%+ coverage

---

## 🚀 Phase 9 - Advanced Caching & Search

### Priority: HIGH | Estimated Time: 8-10 hours | Complexity: Medium-High

### Goals
- 10x faster repeated queries
- 50-70% cost reduction on common queries
- Better retrieval accuracy with hybrid search

### Architecture Overview

```
┌─────────────────────────────────────────────────┐
│           Query Pipeline                        │
│                                                 │
│  1. Query → Semantic Cache Check                │
│     └─ Hit? Return cached result (10ms)        │
│     └─ Miss? Continue...                       │
│                                                 │
│  2. Hybrid Search:                              │
│     ├─ Vector Search (Qdrant)                  │
│     └─ BM25 Keyword Search (in-memory)         │
│     → Reciprocal Rank Fusion                   │
│                                                 │
│  3. LLM Generate Answer                         │
│     └─ Cache result in Redis                   │
└─────────────────────────────────────────────────┘
```

### Tasks

#### 1. Redis Integration
**Files**:
- `src/Rag.Core/Abstractions/ICacheService.cs` (new)
- `src/Rag.Infrastructure/Caching/RedisCacheService.cs` (new)
- `src/Rag.Api/Configuration/CacheConfiguration.cs` (new)

```csharp
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}
```

**Implementation**:
- [ ] Add StackExchange.Redis NuGet package
- [ ] Implement distributed cache with Redis
- [ ] Add tenant-aware cache keys (`tenant:{id}:query:{hash}`)
- [ ] Configure TTL strategies (1h for queries, 24h for embeddings)
- [ ] Add cache statistics endpoint

#### 2. Semantic Cache
**Files**:
- `src/Rag.Core/Abstractions/ISemanticCacheService.cs` (new)
- `src/Rag.Infrastructure/Caching/SemanticCacheService.cs` (new)

```csharp
public interface ISemanticCacheService
{
    Task<CachedQueryResult?> FindSimilarQueryAsync(
        string query, 
        float[] queryEmbedding,
        float similarityThreshold = 0.95f,
        string? tenantId = null,
        CancellationToken ct = default);
    
    Task CacheQueryResultAsync(
        string query,
        float[] queryEmbedding,
        string response,
        List<DocumentChunk> sources,
        string? tenantId = null,
        CancellationToken ct = default);
}
```

**Implementation**:
- [ ] Store query embeddings in Qdrant separate collection (`semantic_cache`)
- [ ] On new query: check if similar cached query exists (cosine similarity > 0.95)
- [ ] If hit: return cached response (no LLM call needed!)
- [ ] If miss: process normally and cache result
- [ ] Add cache warming for common queries
- [ ] TTL: 1 hour (configurable)

#### 3. Hybrid Search (Vector + BM25)
**Files**:
- `src/Rag.Core/Abstractions/IHybridSearchService.cs` (new)
- `src/Rag.Infrastructure/Search/HybridSearchService.cs` (new)
- `src/Rag.Infrastructure/Search/BM25Ranker.cs` (new)

```csharp
public interface IHybridSearchService
{
    Task<List<ScoredDocument>> SearchAsync(
        string query,
        float[] queryEmbedding,
        int topK = 10,
        float vectorWeight = 0.7f,
        float keywordWeight = 0.3f,
        string? tenantId = null,
        CancellationToken ct = default);
}
```

**Implementation**:
- [ ] Implement BM25 scoring algorithm
- [ ] Build in-memory inverted index for keyword search
- [ ] Implement Reciprocal Rank Fusion (RRF) for result merging
- [ ] Make vector/keyword weights configurable
- [ ] Cache document tokens for BM25 (Redis)

#### 4. Cache Warming & Management
**File**: `src/Rag.Infrastructure/Caching/CacheWarmingService.cs` (new)

- [ ] Background job to warm popular queries
- [ ] Analyze query logs to identify top 50 queries
- [ ] Pre-compute embeddings and cache results
- [ ] Admin endpoint to manually warm cache
- [ ] Cache invalidation when documents are updated/deleted

#### 5. Configuration
**File**: `appsettings.json`

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379",
    "InstanceName": "rag-poc:",
    "DefaultTTL": "01:00:00"
  },
  "SemanticCache": {
    "Enabled": true,
    "SimilarityThreshold": 0.95,
    "TTL": "01:00:00",
    "MaxCacheSize": 10000
  },
  "HybridSearch": {
    "Enabled": true,
    "VectorWeight": 0.7,
    "KeywordWeight": 0.3,
    "UseRecipocalRankFusion": true
  }
}
```

#### 6. Monitoring & Metrics
**File**: `src/Rag.Api/Controllers/CacheController.cs` (new)

- [ ] Endpoint: `GET /api/v1/cache/stats`
  - Cache hit rate
  - Average response time (cached vs uncached)
  - Total queries served from cache
  - Cost savings estimate
- [ ] Endpoint: `POST /api/v1/cache/warm`
- [ ] Endpoint: `DELETE /api/v1/cache/clear`

### NuGet Packages
```xml
<PackageReference Include="StackExchange.Redis" Version="2.7.10" />
<PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="8.0.0" />
```

### Deliverables
- Working Redis distributed cache
- Semantic cache with 90%+ hit rate for similar queries
- Hybrid search improving retrieval by 20-30%
- Cache management API
- Performance benchmarks showing 10x improvement

---

## 🤖 Phase 10 - Agent Tool Expansion

### Priority: MEDIUM | Estimated Time: 10-12 hours | Complexity: Medium

### Goals
- Expand agent capabilities with 6+ new tools
- Enable web scraping, SQL queries, math evaluation
- Add long-term conversation memory

### New Tools

#### 1. Web Scraping Tool
**File**: `src/Rag.Infrastructure/Agent/Tools/WebScrapingTool.cs`

```csharp
[Tool("web_scrape")]
[Category(ToolCategory.Web)]
public class WebScrapingTool : IAgentTool
{
    // Use Playwright or HtmlAgilityPack
    // Input: URL
    // Output: Extracted text content
    // Features:
    // - JavaScript rendering (Playwright)
    // - Respect robots.txt
    // - Rate limiting
    // - Link extraction
}
```

**Implementation**:
- [ ] Add Playwright or HtmlAgilityPack NuGet package
- [ ] Implement HTML to text extraction
- [ ] Add robots.txt checking
- [ ] Rate limiting per domain
- [ ] Add to tool registry

#### 2. Web Search Tool (DuckDuckGo/Brave)
**File**: `src/Rag.Infrastructure/Agent/Tools/WebSearchTool.cs`

```csharp
[Tool("web_search")]
[Category(ToolCategory.Web)]
public class WebSearchTool : IAgentTool
{
    // Input: search query
    // Output: Top 5 results with title, snippet, URL
    // Use: DuckDuckGo API (free) or Brave Search API
}
```

#### 3. SQL Query Tool (Read-Only)
**File**: `src/Rag.Infrastructure/Agent/Tools/SqlQueryTool.cs`

```csharp
[Tool("sql_query")]
[Category(ToolCategory.Database)]
public class SqlQueryTool : IAgentTool
{
    // Safety features:
    // - Parse and validate SQL (no INSERT/UPDATE/DELETE/DROP)
    // - Query timeout (5 seconds)
    // - Row limit (100 rows max)
    // - Parameterized queries only
    // - Whitelist tables
}
```

**Implementation**:
- [ ] SQL parser to validate read-only queries
- [ ] Connection pooling
- [ ] Query timeout enforcement
- [ ] Result limiting
- [ ] Schema introspection tool

#### 4. Calculator/Math Tool
**File**: `src/Rag.Infrastructure/Agent/Tools/CalculatorTool.cs`

```csharp
[Tool("calculator")]
[Category(ToolCategory.Utility)]
public class CalculatorTool : IAgentTool
{
    // Use: NCalc or Math.NET
    // Input: Mathematical expression
    // Output: Numerical result
    // Supports: +, -, *, /, ^, sin, cos, log, etc.
}
```

#### 5. File System Tool (Read-Only)
**File**: `src/Rag.Infrastructure/Agent/Tools/FileSystemTool.cs`

```csharp
[Tool("file_read")]
[Category(ToolCategory.System)]
public class FileSystemTool : IAgentTool
{
    // Read local files for analysis
    // Security: sandboxed directory only
    // Supported formats: txt, csv, json, xml, md
}
```

#### 6. Email Tool (Send)
**File**: `src/Rag.Infrastructure/Agent/Tools/EmailTool.cs`

```csharp
[Tool("send_email")]
[Category(ToolCategory.Communication)]
public class EmailTool : IAgentTool
{
    // Use: MailKit or SendGrid
    // Features: template support, attachments
}
```

#### 7. Long-Term Memory
**Files**:
- `src/Rag.Infrastructure/Agent/Memory/ConversationMemoryService.cs` (new)
- `src/Rag.Core/Agent/IConversationMemoryService.cs` (new)

```csharp
public interface IConversationMemoryService
{
    Task SaveConversationAsync(string userId, string conversationId, List<AgentMessage> messages, CancellationToken ct);
    Task<List<AgentMessage>> GetConversationHistoryAsync(string userId, string conversationId, int limit = 50, CancellationToken ct);
    Task<List<ConversationSummary>> GetRecentConversationsAsync(string userId, int limit = 10, CancellationToken ct);
    Task<string> SummarizeConversationAsync(List<AgentMessage> messages, CancellationToken ct);
}
```

**Storage**:
- PostgreSQL or SQL Server for conversation history
- Redis for session state
- Periodic summarization of long conversations

### Testing
- [ ] Unit tests for each tool
- [ ] Integration tests with agent orchestrator
- [ ] Security tests (SQL injection, path traversal)
- [ ] Rate limiting tests

### Deliverables
- 6+ production-ready agent tools
- Tool documentation and examples
- Security audit report
- Demo scenarios using new tools

---

## 🏢 Phase 11 - Production Infrastructure

### Priority: MEDIUM | Estimated Time: 12-16 hours | Complexity: HIGH

### Goals
- Replace in-memory storage with SQL database
- Add OpenTelemetry distributed tracing
- Production-grade observability
- Database migrations

### Architecture Changes

```
BEFORE (In-Memory):
┌──────────────────────────────────────────┐
│  Dictionary<TenantId, List<Document>>    │  ← Lost on restart
│  Dictionary<UserId, ApiUsage>            │  ← No persistence
│  Hangfire MemoryStorage                  │  ← Lost on restart
└──────────────────────────────────────────┘

AFTER (Database):
┌──────────────────────────────────────────┐
│  SQL Server / PostgreSQL                 │
│  ├─ Documents (metadata)                 │
│  ├─ Users & Tenants                      │
│  ├─ ApiUsage & CostTracking              │
│  ├─ EvaluationTestCases                  │
│  ├─ EvaluationRuns                       │
│  ├─ ConversationHistory                  │
│  └─ HangfireJobs                         │
│                                          │
│  OpenTelemetry → Jaeger/Zipkin           │
└──────────────────────────────────────────┘
```

### Tasks

#### 1. Database Layer with EF Core
**Files**:
- `src/Rag.Infrastructure/Data/RagDbContext.cs` (new)
- `src/Rag.Infrastructure/Data/Entities/*.cs` (new)
- `src/Rag.Infrastructure/Data/Repositories/*.cs` (new)

**Entities**:
```csharp
public class DocumentEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
    public int ChunkCount { get; set; }
    public long SizeBytes { get; set; }
}

public class TenantEntity
{
    public string Id { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string PlanType { get; set; } // Free, Pro, Enterprise
    public int MonthlyQueryLimit { get; set; }
    public int CurrentQueryCount { get; set; }
}

public class ApiUsageEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; }
    public string UserId { get; set; }
    public string Endpoint { get; set; }
    public DateTime Timestamp { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal Cost { get; set; }
    public int DurationMs { get; set; }
}

public class ConversationEntity
{
    public Guid Id { get; set; }
    public string UserId { get; set; }
    public string TenantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Title { get; set; }
    public List<MessageEntity> Messages { get; set; }
}
```

#### 2. EF Core Migrations
**Commands**:
```bash
dotnet ef migrations add InitialCreate --project src/Rag.Infrastructure
dotnet ef database update --project src/Rag.Infrastructure
```

**Files**:
- `src/Rag.Infrastructure/Migrations/*.cs` (auto-generated)

#### 3. Repository Pattern
```csharp
public interface IDocumentRepository
{
    Task<DocumentEntity?> GetByIdAsync(Guid id, string tenantId, CancellationToken ct);
    Task<List<DocumentEntity>> GetAllAsync(string tenantId, int skip, int take, CancellationToken ct);
    Task<Guid> CreateAsync(DocumentEntity document, CancellationToken ct);
    Task UpdateAsync(DocumentEntity document, CancellationToken ct);
    Task DeleteAsync(Guid id, string tenantId, CancellationToken ct);
}
```

#### 4. OpenTelemetry Integration
**Files**:
- `src/Rag.Api/Configuration/TelemetryConfiguration.cs` (new)

**NuGet Packages**:
```xml
<PackageReference Include="OpenTelemetry" Version="1.7.0" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.7.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.7.1" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.7.1" />
<PackageReference Include="OpenTelemetry.Instrumentation.SqlClient" Version="1.7.0-beta.1" />
```

**Implementation**:
```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
        tracerProviderBuilder
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation()
            .AddSource("Rag.Api")
            .AddSource("Rag.Infrastructure")
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://localhost:4317"); // Jaeger
            }))
    .WithMetrics(meterProviderBuilder =>
        meterProviderBuilder
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter("Rag.Api")
            .AddOtlpExporter());
```

**Custom Spans**:
```csharp
using var activity = ActivitySource.StartActivity("RAG.Query");
activity?.SetTag("query", question);
activity?.SetTag("tenant_id", tenantId);
activity?.SetTag("top_k", topK);
// ... perform RAG query
activity?.SetTag("num_results", hits.Count);
```

#### 5. Persistent Hangfire Storage
**Configuration**:
```csharp
builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(
        builder.Configuration.GetConnectionString("Hangfire"),
        new SqlServerStorageOptions
        {
            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            QueuePollInterval = TimeSpan.Zero,
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks = true
        }));
```

#### 6. Structured Logging with Serilog
**File**: `src/Rag.Api/Program.cs`

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(new JsonFormatter())
    .WriteTo.File(
        new JsonFormatter(),
        path: "logs/rag-api-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .WriteTo.Seq("http://localhost:5341") // Optional: Seq for log aggregation
    .CreateLogger();
```

#### 7. Health Checks Enhancement
**File**: `src/Rag.Api/HealthChecks/DatabaseHealthCheck.cs`

```csharp
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly RagDbContext _dbContext;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.Database.CanConnectAsync(cancellationToken);
            return HealthCheckResult.Healthy("Database connection is healthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed", ex);
        }
    }
}
```

### Configuration
**appsettings.Production.json**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=RagPoc;Trusted_Connection=True;",
    "Hangfire": "Server=localhost;Database=RagPocHangfire;Trusted_Connection=True;"
  },
  "OpenTelemetry": {
    "ServiceName": "rag-poc-api",
    "ServiceVersion": "1.0.0",
    "ExporterEndpoint": "http://localhost:4317"
  }
}
```

### Docker Compose for Local Dev
**File**: `docker-compose.yml` (update)

```yaml
version: '3.8'
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      SA_PASSWORD: "YourStrong@Passw0rd"
      ACCEPT_EULA: "Y"
    ports:
      - "1433:1433"

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"

  jaeger:
    image: jaegertracing/all-in-one:latest
    ports:
      - "16686:16686"  # Jaeger UI
      - "4317:4317"    # OTLP gRPC
      - "4318:4318"    # OTLP HTTP

  seq:
    image: datalust/seq:latest
    environment:
      ACCEPT_EULA: "Y"
    ports:
      - "5341:80"
```

### Deliverables
- Working SQL database with migrations
- OpenTelemetry tracing with Jaeger UI
- Persistent Hangfire jobs
- Structured logging with Serilog
- Docker Compose for full stack
- Production deployment guide

---

## 📅 Timeline & Milestones

### Week 1: Complete Phase 8 + Start Phase 9
- Days 1-2: Agent streaming endpoint, WebSocket support
- Days 3-4: Integration tests, documentation
- Days 5-7: Redis integration, basic caching

### Week 2: Complete Phase 9
- Days 1-3: Semantic cache implementation
- Days 4-5: Hybrid search (BM25 + Vector)
- Days 6-7: Cache warming, metrics, testing

### Week 3: Phase 10 - Agent Tools
- Days 1-2: Web scraping + web search tools
- Days 3-4: SQL query + calculator tools
- Days 5-7: Long-term memory, testing

### Week 4: Phase 11 - Production Infrastructure
- Days 1-3: Database migration, EF Core setup
- Days 4-5: OpenTelemetry, distributed tracing
- Days 6-7: Docker Compose, documentation

**Total Estimated Time**: 4 weeks (100-120 hours)

---

## 📚 Documentation Updates Required

1. **API Documentation**: Update OpenAPI specs for new endpoints
2. **README.md**: Add Phase 8-11 features
3. **ARCHITECTURE-DIAGRAMS.md**: Update with caching and database layers
4. **DEPLOYMENT-GUIDE.md**: New file with production deployment steps
5. **PERFORMANCE-BENCHMARKS.md**: New file with caching metrics

---

## 🧪 Testing Strategy

### Unit Tests (80% coverage)
- All new services and tools
- Cache logic
- Database repositories

### Integration Tests
- End-to-end streaming flows
- Cache hit/miss scenarios
- Hybrid search accuracy
- Database persistence

### Performance Tests
- Cache performance (10x improvement)
- Streaming latency (<100ms first token)
- Database query performance
- Concurrent user load

### Security Tests
- SQL injection prevention
- Path traversal prevention
- Rate limiting effectiveness
- Authentication/authorization

---

## 🎯 Success Criteria

### Phase 8
- ✅ Agent streaming works with tool call visualization
- ✅ WebSocket bidirectional chat functional
- ✅ <100ms time to first token
- ✅ 90% uptime for streaming connections

### Phase 9
- ✅ 90%+ cache hit rate for similar queries
- ✅ 10x faster response for cached queries
- ✅ 50-70% cost reduction
- ✅ Hybrid search improves accuracy by 20%+

### Phase 10
- ✅ 6+ new tools working reliably
- ✅ Zero security vulnerabilities
- ✅ <500ms average tool execution time
- ✅ Conversation memory persists across sessions

### Phase 11
- ✅ Database migrations work flawlessly
- ✅ OpenTelemetry traces all requests
- ✅ <1% data loss
- ✅ Production deployment successful

---

## 🚨 Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Redis availability | High | Implement fallback to in-memory cache |
| Database migration issues | High | Test migrations thoroughly in staging |
| WebSocket scaling | Medium | Use SignalR backplane (Redis) |
| SQL injection in SQL tool | Critical | Parse and whitelist queries strictly |
| Cache stampede | Medium | Implement distributed locks |

---

## 📈 Expected Business Impact

1. **Performance**: 10x faster responses (Phase 9)
2. **Cost**: 50-70% reduction in LLM costs (Phase 9)
3. **Capabilities**: 3x more use cases (Phase 10)
4. **Reliability**: 99.9% uptime (Phase 11)
5. **Observability**: Full request tracing (Phase 11)

---

**Next Steps**: Start Phase 8 completion with agent streaming endpoint.
