# Phase 11: Production Infrastructure 🏢

**Priority**: MEDIUM | **Complexity**: HIGH | **Status**: NOT STARTED

## Overview
Transform the POC into production-grade infrastructure with persistent database, distributed tracing, and enterprise observability.

---

## Goals
- ✅ Database migration from in-memory to SQL Server/PostgreSQL
- ✅ OpenTelemetry distributed tracing
- ✅ Persistent Hangfire storage
- ✅ EF Core migrations
- ✅ Connection pooling and resilience
- ✅ Health checks and readiness probes

---

## Current State (POC)
- ❌ In-memory configuration storage
- ❌ No persistent job storage (Hangfire)
- ❌ Basic logging only
- ❌ No distributed tracing
- ❌ Manual database setup

---

## Target State (Production)

### Architecture
```
┌─────────────────────────────────────────────────────┐
│                   API Layer                         │
│  - Health checks                                    │
│  - Middleware pipeline                              │
│  - OpenTelemetry instrumentation                    │
└──────────────────┬──────────────────────────────────┘
                   │
       ┌───────────┼───────────┐
       │           │           │
       ▼           ▼           ▼
┌──────────┐ ┌──────────┐ ┌──────────┐
│   SQL    │ │  Qdrant  │ │  Redis   │
│ Database │ │  Vector  │ │  Cache   │
└──────────┘ └──────────┘ └──────────┘
       │
       ▼
┌──────────────────────────────────┐
│  OpenTelemetry Collector         │
│  → Jaeger (traces)               │
│  → Prometheus (metrics)          │
│  → Loki (logs)                   │
└──────────────────────────────────┘
```

---

## Implementation Tasks

### Task 1: Database Setup & EF Core Migration

#### 1.1 Choose Database
**Options**:
- **SQL Server** (Azure SQL, good for .NET ecosystem)
- **PostgreSQL** (open-source, excellent performance)
- **MySQL** (lightweight, good compatibility)

**Recommendation**: PostgreSQL for production, SQL Server for enterprise

#### 1.2 Create Database Context
**Files to create**:
- `src/Rag.Infrastructure/Data/RagDbContext.cs`
- `src/Rag.Infrastructure/Data/Entities/ApiKeyEntity.cs`
- `src/Rag.Infrastructure/Data/Entities/UserEntity.cs`
- `src/Rag.Infrastructure/Data/Entities/CostTrackingEntity.cs`
- `src/Rag.Infrastructure/Data/Entities/HangfireJobEntity.cs`

**Example DbContext**:
```csharp
public class RagDbContext : DbContext
{
    public RagDbContext(DbContextOptions<RagDbContext> options) : base(options) { }

    public DbSet<ApiKeyEntity> ApiKeys { get; set; }
    public DbSet<UserEntity> Users { get; set; }
    public DbSet<TenantEntity> Tenants { get; set; }
    public DbSet<CostUsageEntity> CostUsage { get; set; }
    public DbSet<ConversationEntity> Conversations { get; set; }
    public DbSet<DocumentEntity> Documents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApiKeyEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.HasIndex(e => e.TenantId);
            entity.Property(e => e.Key).HasMaxLength(128);
        });

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.TenantId);
        });

        modelBuilder.Entity<CostUsageEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.Timestamp });
            entity.Property(e => e.Cost).HasPrecision(18, 6);
        });
    }
}
```

#### 1.3 EF Core Migrations
**Commands**:
```powershell
# Install EF Core tools
dotnet tool install --global dotnet-ef

# Add migration
dotnet ef migrations add InitialCreate --project src/Rag.Infrastructure --startup-project src/Rag.Api

# Update database
dotnet ef database update --project src/Rag.Infrastructure --startup-project src/Rag.Api

# Generate SQL script
dotnet ef migrations script --project src/Rag.Infrastructure --output migrations.sql
```

**NuGet Packages**:
```xml
<!-- PostgreSQL -->
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.0" />

<!-- SQL Server -->
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="9.0.0" />
```

---

### Task 2: Persistent Hangfire Storage

#### Current State
```csharp
// In-memory (lost on restart)
services.AddHangfire(config => config.UseInMemoryStorage());
```

#### Production State
**For SQL Server**:
```csharp
services.AddHangfire(config => config
    .UseSqlServerStorage(configuration.GetConnectionString("Hangfire"), new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true,
        SchemaName = "hangfire"
    }));
```

**For PostgreSQL**:
```csharp
services.AddHangfire(config => config
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(
        configuration.GetConnectionString("Hangfire")), 
        new PostgreSqlStorageOptions
        {
            SchemaName = "hangfire",
            QueuePollInterval = TimeSpan.FromSeconds(15)
        }));
```

**NuGet Packages**:
```xml
<PackageReference Include="Hangfire.SqlServer" Version="1.8.9" />
<!-- OR -->
<PackageReference Include="Hangfire.PostgreSql" Version="1.20.8" />
```

---

### Task 3: OpenTelemetry Distributed Tracing

#### 3.1 Install OpenTelemetry
**NuGet Packages**:
```xml
<PackageReference Include="OpenTelemetry" Version="1.7.0" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.7.0" />
<PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.7.0" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.7.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.7.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.7.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.SqlClient" Version="1.7.0-beta.1" />
<PackageReference Include="OpenTelemetry.Instrumentation.StackExchangeRedis" Version="1.0.0-rc9.14" />
```

#### 3.2 Configure OpenTelemetry
**File**: `src/Rag.Api/Configuration/OpenTelemetryConfiguration.cs`

```csharp
public static class OpenTelemetryConfiguration
{
    public static IServiceCollection AddOpenTelemetryObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "RagPoc";
        var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";

        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(serviceName)
                        .AddTelemetrySdk()
                        .AddAttributes(new Dictionary<string, object>
                        {
                            ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"
                        }))
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = (httpContext) =>
                        {
                            // Don't trace health check endpoints
                            return !httpContext.Request.Path.StartsWithSegments("/health");
                        };
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddSqlClientInstrumentation(options =>
                    {
                        options.SetDbStatementForText = true;
                        options.RecordException = true;
                    })
                    .AddRedisInstrumentation()
                    .AddSource("Rag.*") // Custom activity sources
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                        options.Protocol = OtlpExportProtocol.Grpc;
                    })
                    .AddConsoleExporter(); // For development
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddMeter("Rag.*") // Custom meters
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                        options.Protocol = OtlpExportProtocol.Grpc;
                    })
                    .AddConsoleExporter();
            });

        return services;
    }
}
```

#### 3.3 Add Custom Tracing
**File**: `src/Rag.Core/Services/TracingService.cs`

```csharp
public class TracingService
{
    private static readonly ActivitySource ActivitySource = new("Rag.Core");

    public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
    {
        return ActivitySource.StartActivity(name, kind);
    }

    public static void AddTag(Activity? activity, string key, object? value)
    {
        activity?.SetTag(key, value);
    }

    public static void RecordException(Activity? activity, Exception ex)
    {
        activity?.RecordException(ex);
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    }
}
```

**Usage Example**:
```csharp
public async Task<AskResponse> Ask(AskRequest request)
{
    using var activity = TracingService.StartActivity("AskController.Ask");
    activity?.SetTag("question", request.Question);
    activity?.SetTag("tenant_id", _tenantContext.TenantId);

    try
    {
        // ... existing code ...
        
        activity?.SetTag("hits_count", hits.Count);
        activity?.SetTag("embedding_tokens", embeddingResult.TokenUsage);
        
        return response;
    }
    catch (Exception ex)
    {
        TracingService.RecordException(activity, ex);
        throw;
    }
}
```

---

### Task 4: Enhanced Health Checks

**File**: `src/Rag.Api/HealthChecks/DatabaseHealthCheck.cs`

```csharp
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly RagDbContext _dbContext;

    public DatabaseHealthCheck(RagDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.Database.CanConnectAsync(cancellationToken);
            return HealthCheckResult.Healthy("Database is accessible");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed", ex);
        }
    }
}
```

**Registration in Program.cs**:
```csharp
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<QdrantHealthCheck>("qdrant")
    .AddCheck<OpenAiHealthCheck>("openai")
    .AddCheck<ClaudeHealthCheck>("claude")
    .AddRedis(configuration.GetConnectionString("Redis")!, "redis")
    .AddHangfire(options => options.MinimumAvailableServers = 1, "hangfire");

// Add health checks UI
builder.Services.AddHealthChecksUI()
    .AddInMemoryStorage();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
app.MapHealthChecksUI(options => options.UIPath = "/health-ui");
```

**NuGet Packages**:
```xml
<PackageReference Include="AspNetCore.HealthChecks.UI" Version="8.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.UI.Client" Version="8.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.UI.InMemory.Storage" Version="8.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.Redis" Version="8.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.Hangfire" Version="8.0.0" />
```

---

### Task 5: Configuration Management

**appsettings.Production.json**:
```json
{
  "ConnectionStrings": {
    "RagDatabase": "Host=prod-db.postgres.azure.com;Database=ragpoc;Username=admin;Password=***;SSL Mode=Require;",
    "Hangfire": "Host=prod-db.postgres.azure.com;Database=ragpoc_hangfire;Username=admin;Password=***;SSL Mode=Require;",
    "Redis": "prod-redis.redis.cache.windows.net:6380,ssl=true,password=***"
  },
  "OpenTelemetry": {
    "ServiceName": "RagPoc-Production",
    "OtlpEndpoint": "http://otel-collector:4317",
    "Enabled": true
  },
  "Qdrant": {
    "Host": "https://prod-qdrant.azurewebsites.net",
    "ApiKey": "***",
    "Collection": "rag_documents_prod"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "/var/log/ragpoc/log-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      }
    ]
  }
}
```

---

### Task 6: Docker Compose for Local Production Testing

**docker-compose.production.yml**:
```yaml
version: '3.8'

services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: ragpoc
      POSTGRES_USER: admin
      POSTGRES_PASSWORD: dev_password
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U admin"]
      interval: 10s
      timeout: 5s
      retries: 5

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    command: redis-server --save 60 1 --loglevel warning
    volumes:
      - redis_data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 3s
      retries: 5

  qdrant:
    image: qdrant/qdrant:latest
    ports:
      - "6333:6333"
      - "6334:6334"
    volumes:
      - qdrant_data:/qdrant/storage
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:6333/health"]
      interval: 10s
      timeout: 3s
      retries: 5

  jaeger:
    image: jaegertracing/all-in-one:latest
    ports:
      - "5775:5775/udp"
      - "6831:6831/udp"
      - "6832:6832/udp"
      - "5778:5778"
      - "16686:16686"  # Jaeger UI
      - "14268:14268"
      - "14250:14250"
      - "9411:9411"
    environment:
      COLLECTOR_OTLP_ENABLED: true

  otel-collector:
    image: otel/opentelemetry-collector-contrib:latest
    command: ["--config=/etc/otel-collector-config.yaml"]
    volumes:
      - ./otel-collector-config.yaml:/etc/otel-collector-config.yaml
    ports:
      - "4317:4317"   # OTLP gRPC
      - "4318:4318"   # OTLP HTTP
      - "8888:8888"   # Prometheus metrics
      - "13133:13133" # health_check
    depends_on:
      - jaeger

  ragpoc-api:
    build:
      context: .
      dockerfile: Dockerfile
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__RagDatabase: "Host=postgres;Database=ragpoc;Username=admin;Password=dev_password"
      ConnectionStrings__Redis: "redis:6379"
      Qdrant__Host: "http://qdrant:6333"
      OpenTelemetry__OtlpEndpoint: "http://otel-collector:4317"
    ports:
      - "5129:8080"
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
      qdrant:
        condition: service_healthy
      otel-collector:
        condition: service_started

volumes:
  postgres_data:
  redis_data:
  qdrant_data:
```

---

## Database Schema

### Core Tables

**ApiKeys**
```sql
CREATE TABLE api_keys (
    id UUID PRIMARY KEY,
    key VARCHAR(128) UNIQUE NOT NULL,
    tenant_id VARCHAR(50) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL,
    expires_at TIMESTAMP,
    last_used_at TIMESTAMP
);
CREATE INDEX idx_apikeys_tenant ON api_keys(tenant_id);
```

**Users**
```sql
CREATE TABLE users (
    id UUID PRIMARY KEY,
    email VARCHAR(255) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    tenant_id VARCHAR(50) NOT NULL,
    role VARCHAR(50) NOT NULL,
    created_at TIMESTAMP NOT NULL
);
CREATE INDEX idx_users_tenant ON users(tenant_id);
```

**CostUsage**
```sql
CREATE TABLE cost_usage (
    id BIGSERIAL PRIMARY KEY,
    tenant_id VARCHAR(50) NOT NULL,
    user_id UUID,
    operation VARCHAR(100) NOT NULL,
    model VARCHAR(100),
    input_tokens INTEGER,
    output_tokens INTEGER,
    cost DECIMAL(18,6) NOT NULL,
    timestamp TIMESTAMP NOT NULL
);
CREATE INDEX idx_costusage_tenant_time ON cost_usage(tenant_id, timestamp DESC);
```

**Conversations** (for memory tool)
```sql
CREATE TABLE conversations (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL,
    tenant_id VARCHAR(50) NOT NULL,
    title VARCHAR(500),
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NOT NULL
);

CREATE TABLE conversation_messages (
    id BIGSERIAL PRIMARY KEY,
    conversation_id UUID NOT NULL REFERENCES conversations(id) ON DELETE CASCADE,
    role VARCHAR(20) NOT NULL,
    content TEXT NOT NULL,
    timestamp TIMESTAMP NOT NULL
);
CREATE INDEX idx_messages_conversation ON conversation_messages(conversation_id, timestamp);
```

---

## Testing Strategy

### Integration Tests
```csharp
public class DatabaseIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task DatabaseConnection_ShouldBeHealthy()
    {
        // Arrange
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EfMigrations_ShouldApplySuccessfully()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RagDbContext>();

        // Act
        await dbContext.Database.MigrateAsync();

        // Assert
        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().NotBeEmpty();
    }
}
```

---

## Deployment Checklist

- [ ] Database provisioned (Azure SQL/AWS RDS/Google Cloud SQL)
- [ ] Connection strings configured
- [ ] EF migrations applied
- [ ] Hangfire database initialized
- [ ] Redis instance running
- [ ] OpenTelemetry collector deployed
- [ ] Health checks passing
- [ ] Backup strategy configured
- [ ] Monitoring alerts set up
- [ ] Load testing completed
- [ ] Security audit passed

---

## Success Criteria
- ✅ All data persisted to database
- ✅ Zero data loss on application restart
- ✅ Distributed tracing showing full request path
- ✅ Health checks reporting accurate status
- ✅ Database migrations automated
- ✅ Connection pooling optimized
- ✅ Observability dashboard functional

---

## Cost Estimation (Monthly)

| Resource | Tier | Cost |
|----------|------|------|
| Azure SQL Database | Basic (2GB) | $5 |
| Azure Cache for Redis | Basic (1GB) | $15 |
| Azure Monitor/App Insights | Pay-as-you-go | $50-100 |
| **Total** | | **$70-120/month** |

---

## Migration Steps

1. **Week 1**: Database setup + EF migrations
2. **Week 2**: Hangfire persistent storage
3. **Week 3**: OpenTelemetry implementation
4. **Week 4**: Testing + optimization
5. **Week 5**: Production deployment

---

## Next Steps
After completion → System is **production-ready**! 🎉
Consider: Kubernetes deployment, auto-scaling, multi-region setup
