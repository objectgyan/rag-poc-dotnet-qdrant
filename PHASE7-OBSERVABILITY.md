# 📊 Phase 7 - Observability, Logging & Monitoring

This document describes the observability infrastructure implemented in Phase 7 to enable production monitoring, debugging, and operational excellence.

## ✅ What's Been Implemented

### 1️⃣ **Structured Logging with Serilog**

**Purpose**: Replace Console.WriteLine with production-grade structured logging for better debugging and analysis.

**Implementation**:
- Added **Serilog.AspNetCore (v9.0.0)** with multiple sinks
- Configured early in Program.cs (before app startup)
- Automatic request logging with enrichers
- File-based logging with daily rolling retention

**Packages Added**:
- `Serilog.AspNetCore` (9.0.0)
- `Serilog.Sinks.Console` (6.0.0)
- `Serilog.Sinks.File` (6.0.0)
- `Serilog.Enrichers.Environment` (3.0.1)
- `Serilog.Enrichers.Thread` (4.0.0)

**Log Configuration**:
```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("Application", "RAG-API")
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
    )
    .WriteTo.File(
        path: "logs/rag-api-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
    )
    .CreateLogger();
```

**Log Enrichers**:
- `FromLogContext` - Adds contextual properties from the scope
- `WithMachineName` - Includes the machine name for distributed systems
- `WithThreadId` - Adds thread ID for debugging concurrency issues
- Custom `Application` property for filtering logs

**Log Retention**:
- Daily rolling logs: `logs/rag-api-20260214.log`
- Retained for 30 days automatically
- Structured format with JSON properties for easy parsing

**Example Log Output**:
```
2026-02-14 14:32:15.234 -07:00 [INF] HTTP POST /api/v1/ask responded 200 in 1234ms {"RequestId":"0HMVFE42N8T5K:00000001","TenantId":"tenant-123","UserId":"user-456","TokensUsed":150,"Cost":0.0015}
```

**Files Modified**:
- `src/Rag.Api/Program.cs` - Added Serilog configuration and `UseSerilog()`
- `src/Rag.Infrastructure/Agent/CodebaseIngestionService.cs` - Replaced `Console.WriteLine` with `ILogger`

---

### 2️⃣ **Global Exception Handling Middleware**

**Purpose**: Catch all unhandled exceptions and return standardized RFC 7807 ProblemDetails responses.

**Implementation**:
- Created `GlobalExceptionMiddleware` with structured error responses
- Returns RFC 7807 ProblemDetails format for consistency
- Hides sensitive error details in production
- Includes trace ID and correlation ID for support tracking
- Logs exceptions with full stack traces

**Error Response Format**:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "An error occurred while processing your request",
  "status": 500,
  "instance": "/api/v1/ask",
  "detail": "An internal error occurred. Please contact support with the trace ID.",
  "traceId": "0HMVFE42N8T5K:00000001",
  "correlationId": "abc123"
}
```

**Development vs Production**:
- **Development**: Shows full error message and stack trace
- **Production**: Hides error details, only shows trace ID

**Files Created**:
- `src/Rag.Api/Middleware/GlobalExceptionMiddleware.cs`

**Files Modified**:
- `src/Rag.Api/Program.cs` - Replaced `UseExceptionHandler` with `UseGlobalExceptionHandler()`

**Example Usage**:
```csharp
// Middleware automatically catches all exceptions
app.UseGlobalExceptionHandler();
```

---

### 3️⃣ **Health Check Endpoints**

**Purpose**: Monitor dependency health for load balancers, orchestrators, and operations teams.

**Implementation**:
- Added ASP.NET Core Health Checks with custom health check classes
- Three endpoints: `/health`, `/health/live`, `/health/ready`
- Checks for Qdrant, Claude API, and OpenAI API availability
- JSON response format with detailed status per dependency

**Health Check Endpoints**:

#### `/health` - Comprehensive Health Check
Returns status of all dependencies with timing information.

**Response Format**:
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:01.234",
  "entries": {
    "qdrant": {
      "status": "Healthy",
      "duration": "00:00:00.123",
      "description": "Qdrant is reachable"
    },
    "claude": {
      "status": "Healthy",
      "duration": "00:00:00.456",
      "description": "Claude API is reachable",
      "ResponseTime": "456ms",
      "Model": "claude-sonnet-4-20250514"
    },
    "openai": {
      "status": "Healthy",
      "duration": "00:00:00.234",
      "description": "OpenAI API is reachable",
      "ResponseTime": "234ms",
      "EmbeddingModel": "text-embedding-3-small"
    }
  }
}
```

#### `/health/live` - Liveness Probe
Simple endpoint that returns 200 OK if the application is running (no dependency checks).

**Use Case**: Kubernetes liveness probes, Docker health checks

#### `/health/ready` - Readiness Probe
Checks critical dependencies (Qdrant, Claude, OpenAI) before returning 200 OK.

**Use Case**: Load balancer readiness checks, Kubernetes readiness probes

**Health Check Classes**:

#### `QdrantHealthCheck`
- Checks Qdrant connectivity via gRPC client
- Timeout: 5 seconds
- Tags: `database`, `vector`

#### `ClaudeHealthCheck`
- Checks Claude API availability via HTTP request
- Timeout: 5 seconds
- Tags: `api`, `llm`
- Returns model information

#### `OpenAiHealthCheck`
- Checks OpenAI API availability via models endpoint
- Timeout: 5 seconds
- Tags: `api`, `embeddings`
- Returns embedding model information

**Files Created**:
- `src/Rag.Api/HealthChecks/ClaudeHealthCheck.cs`
- `src/Rag.Api/HealthChecks/OpenAiHealthCheck.cs`

**Files Modified**:
- `src/Rag.Api/Program.cs` - Added health check configuration and endpoints

**Packages Added**:
- `AspNetCore.HealthChecks.Qdrant` (9.0.1)

---

### 4️⃣ **Request Logging & Correlation IDs**

**Purpose**: Track requests across the system for debugging distributed systems.

**Implementation**:
- Serilog automatically logs HTTP requests with duration
- Trace ID included in all log entries (via `HttpContext.TraceIdentifier`)
- Correlation ID support for distributed tracing
- Request/response logging with status codes and timing

**Logged Request Properties**:
- `RequestId` - ASP.NET Core trace identifier
- `RequestPath` - HTTP path (e.g., `/api/v1/ask`)
- `RequestMethod` - HTTP method (GET, POST, etc.)
- `StatusCode` - HTTP status code
- `Elapsed` - Request duration in milliseconds
- `TenantId` - Multi-tenant context (if available)
- `UserId` - Authenticated user (if available)

**Example Log Entry**:
```json
{
  "@t": "2026-02-14T21:32:15.234Z",
  "@mt": "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed}ms",
  "@l": "Information",
  "RequestMethod": "POST",
  "RequestPath": "/api/v1/ask",
  "StatusCode": 200,
  "Elapsed": 1234,
  "RequestId": "0HMVFE42N8T5K:00000001",
  "TenantId": "tenant-123",
  "UserId": "user-456",
  "Application": "RAG-API",
  "MachineName": "WEB-SERVER-01",
  "ThreadId": 42
}
```

---

## 📊 Observability Architecture

### Logging Flow:
```
1. Request arrives → UseSerilog() captures request details
2. Middleware enriches logs with TenantId, UserId, RequestId
3. Controllers/Services log operations with structured data
4. GlobalExceptionMiddleware catches errors → Logs + RFC 7807 response
5. Response sent → Serilog logs response time and status
6. Logs written to Console + File (daily rolling)
```

### Health Check Flow:
```
1. Load balancer calls /health/ready
2. ASP.NET Health Checks execute:
   - QdrantHealthCheck → gRPC connection test
   - ClaudeHealthCheck → HTTP GET /v1/messages
   - OpenAiHealthCheck → HTTP GET /v1/models
3. Each check runs with 5-second timeout
4. Aggregate status returned as JSON
5. Load balancer routes traffic based on status
```

---

## 🔧 Configuration

### Serilog Configuration (appsettings.json)
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/rag-api-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  }
}
```

**Note**: Serilog configuration can be moved to `appsettings.json` for easier management in different environments.

---

## ✅ Verification & Testing

### 1. Test Structured Logging
```bash
# Start the API
dotnet run --project src/Rag.Api

# Check logs directory created
ls logs/

# Tail logs in real-time
tail -f logs/rag-api-20260214.log

# Make a request
curl -X POST https://localhost:5001/api/v1/ask \
  -H "Content-Type: application/json" \
  -H "X-API-Key: test-key" \
  -H "X-Tenant-Id: tenant-123" \
  -d '{"question": "What is RAG?", "topK": 5}'

# Check logs for structured output
# Expected: [14:32:15 INF] HTTP POST /api/v1/ask responded 200 in 1234ms {"RequestId":"...","TenantId":"tenant-123"}
```

### 2. Test Health Checks
```bash
# Test comprehensive health check
curl https://localhost:5001/health | jq

# Expected JSON response:
# {
#   "status": "Healthy",
#   "totalDuration": "00:00:01.234",
#   "entries": { ... }
# }

# Test liveness probe (always returns 200 if app is running)
curl -I https://localhost:5001/health/live
# Expected: HTTP/1.1 200 OK

# Test readiness probe (checks dependencies)
curl -I https://localhost:5001/health/ready
# Expected: HTTP/1.1 200 OK (if dependencies are healthy)
```

### 3. Test Global Exception Handling
```bash
# Trigger an exception (invalid API key)
curl -X POST https://localhost:5001/api/v1/ask \
  -H "Content-Type: application/json" \
  -H "X-API-Key: invalid-key" \
  -H "X-Tenant-Id: tenant-123" \
  -d '{"question": "test"}' | jq

# Expected: RFC 7807 ProblemDetails response
# {
#   "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
#   "title": "An error occurred while processing your request",
#   "status": 500,
#   "instance": "/api/v1/ask",
#   "detail": "An internal error occurred. Please contact support with the trace ID.",
#   "traceId": "0HMVFE42N8T5K:00000001"
# }

# Check logs for exception details
tail -f logs/rag-api-*.log | grep ERROR
```

### 4. Test Correlation ID Tracking
```bash
# Send request with correlation ID
curl -X POST https://localhost:5001/api/v1/ask \
  -H "Content-Type: application/json" \
  -H "X-API-Key: test-key" \
  -H "X-Tenant-Id: tenant-123" \
  -H "X-Correlation-Id: abc-123-def-456" \
  -d '{"question": "What is RAG?", "topK": 5}'

# Check logs for correlation ID
grep "abc-123-def-456" logs/rag-api-*.log
```

---

## 📈 Monitoring & Operations

### Log Analysis
```bash
# Find all errors in last 24 hours
grep "\\[ERR\\]" logs/rag-api-$(date +%Y%m%d).log

# Count requests by endpoint
grep "HTTP POST" logs/rag-api-*.log | cut -d' ' -f5 | sort | uniq -c

# Find slow requests (>5000ms)
grep "responded 200 in" logs/rag-api-*.log | awk -F'in |ms' '$2 > 5000 {print $0}'

# Track specific tenant activity
grep "tenant-123" logs/rag-api-*.log
```

### Health Check Integration

**Kubernetes Liveness Probe**:
```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 5001
  initialDelaySeconds: 30
  periodSeconds: 10
```

**Kubernetes Readiness Probe**:
```yaml
readinessProbe:
  httpGet:
    path: /health/ready
    port: 5001
  initialDelaySeconds: 15
  periodSeconds: 5
```

**Docker Health Check**:
```dockerfile
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s \
  CMD curl -f https://localhost:5001/health/live || exit 1
```

---

## 🚀 Future Enhancements (Phase 8+)

### Phase 8: Advanced Observability
- **OpenTelemetry Distributed Tracing**
  - Trace requests across services
  - Export to Jaeger/Zipkin/Azure Monitor
  - Automatic instrumentation for HTTP, EF Core, etc.

- **Custom Metrics**
  - Prometheus endpoint (`/metrics`)
  - Request count, latency percentiles (p50, p95, p99)
  - Token usage metrics per tenant
  - Error rate by endpoint

- **Centralized Logging**
  - Serilog.Sinks.Seq for centralized log aggregation
  - Serilog.Sinks.Elasticsearch for log indexing
  - Grafana dashboards for log visualization

- **Application Performance Monitoring (APM)**
  - Azure Application Insights integration
  - Datadog or New Relic integration
  - Real-time performance alerts

---

## 📝 Breaking Changes

⚠️ **None** - Phase 7 is fully backward compatible.

All changes are additive:
- Logging enhanced but existing behavior preserved
- Health check endpoints added (no existing endpoints modified)
- Exception handling improved with same HTTP status codes

---

## 📚 Key Logging Best Practices Applied

✅ **Structured Logging**: All logs use structured format with typed properties
✅ **Log Levels**: Appropriate levels (Information, Warning, Error, Fatal)
✅ **Contextual Enrichment**: TenantId, UserId, RequestId added automatically
✅ **Exception Logging**: Full stack traces logged with context
✅ **Performance Logging**: Request duration tracked for all endpoints
✅ **Security**: Sensitive data (API keys, passwords) never logged
✅ **Retention**: 30-day log retention with daily rolling files

---

## 🔍 Troubleshooting

### Logs not appearing in files
- Check `logs/` directory exists and is writable
- Verify Serilog configuration in Program.cs
- Check for startup errors: `dotnet run --project src/Rag.Api`

### Health checks returning Unhealthy
- **Qdrant Unhealthy**: Verify Qdrant is running at configured URL (`http://localhost:6333`)
- **Claude Unhealthy**: Check API key validity and network connectivity
- **OpenAI Unhealthy**: Check API key validity and rate limits

### Exception middleware not catching errors
- Verify `UseGlobalExceptionHandler()` is first in middleware pipeline
- Check for try-catch blocks in controllers swallowing exceptions

---

## 📊 Phase 7 Summary

**Total Files Created**: 4
**Total Files Modified**: 2
**Build Status**: ✅ SUCCESS (0 errors, 5 warnings)
**Lines of Code Added**: ~800

### New Files:
- `src/Rag.Api/Middleware/GlobalExceptionMiddleware.cs`
- `src/Rag.Api/HealthChecks/ClaudeHealthCheck.cs`
- `src/Rag.Api/HealthChecks/OpenAiHealthCheck.cs`
- `PHASE7-OBSERVABILITY.md`

### Modified Files:
- `src/Rag.Api/Program.cs` - Serilog, health checks, exception middleware
- `src/Rag.Infrastructure/Agent/CodebaseIngestionService.cs` - ILogger injection

### NuGet Packages Added:
- Serilog.AspNetCore (9.0.0)
- Serilog.Sinks.Console (6.0.0)
- Serilog.Sinks.File (6.0.0)
- Serilog.Enrichers.Environment (3.0.1)
- Serilog.Enrichers.Thread (4.0.0)
- AspNetCore.HealthChecks.Qdrant (9.0.1)

---

## 🎉 Phase 7 Complete!

The RAG API now has production-ready observability infrastructure:
- ✅ Structured logging with Serilog (Console + File)
- ✅ Global exception handling with RFC 7807 responses
- ✅ Health check endpoints for Qdrant, Claude, and OpenAI
- ✅ Correlation ID tracking for distributed tracing
- ✅ Request/response logging with timing

**Ready for**: Load balancer integration, Kubernetes deployment, production monitoring

---

## 📚 References

- [Serilog Documentation](https://serilog.net/)
- [RFC 7807: Problem Details for HTTP APIs](https://tools.ietf.org/html/rfc7807)
- [ASP.NET Core Health Checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
- [Structured Logging Best Practices](https://blog.treasuredata.com/blog/2016/11/03/why-structured-logging/)
