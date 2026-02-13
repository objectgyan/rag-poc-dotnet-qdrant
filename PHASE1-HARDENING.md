# 🛡️ Phase 1 - Hardening Implementation

This document describes the production-grade security and resilience features implemented in Phase 1.

## ✅ What's Been Implemented

### 1️⃣ **Rate Limiting**

**Purpose**: Prevent abuse and protect LLM costs by limiting API request frequency.

**Implementation**:
- ASP.NET Core built-in rate limiting with fixed window algorithm
- Separate policies for different endpoints:
  - **`/ask`** endpoint: 30 requests/minute (configurable)
  - **`/ingest`** endpoint: 10 requests/minute (more restrictive for expensive operations)
  - **Global limiter**: 100 requests/minute across all endpoints
- Returns `429 Too Many Requests` with retry-after metadata when exceeded

**Configuration** (`appsettings.json`):
```json
"RateLimiting": {
  "AskRequestsPerMinute": 30,
  "IngestRequestsPerMinute": 10,
  "GlobalRequestsPerMinute": 100
}
```

**Files Added**:
- `src/Rag.Api/Configuration/RateLimitingConfiguration.cs`

**Files Modified**:
- `src/Rag.Api/Controllers/AskController.cs` - Added `[EnableRateLimiting]` attribute
- `src/Rag.Api/Controllers/IngestController.cs` - Added `[EnableRateLimiting]` attribute
- `src/Rag.Api/Program.cs` - Added rate limiting middleware

---

### 2️⃣ **Retry/Backoff Policy (Polly Resilience)**

**Purpose**: Handle transient failures from Claude, OpenAI, and Qdrant with intelligent retry logic.

**Implementation**:
- Microsoft.Extensions.Http.Resilience with Polly v8
- Exponential backoff with jitter to prevent thundering herd
- Separate resilient HTTP clients for:
  - **Claude API** (`ClaudeHttpClient`)
  - **OpenAI API** (`OpenAiHttpClient`)
  - **Qdrant** (`QdrantHttpClient`)
- Automatically retries on:
  - `HttpRequestException` (network errors)
  - `TimeoutException`
  - HTTP 408 (Request Timeout)
  - HTTP 429 (Too Many Requests)
  - HTTP 5xx (Server Errors)
- Logs retry attempts with attempt number and delay

**Configuration** (`appsettings.json`):
```json
"Resilience": {
  "MaxRetryAttempts": 3,
  "InitialRetryDelayMs": 500,
  "TimeoutSeconds": 30
}
```

**Retry Strategy**:
- Attempt 1: Immediate
- Attempt 2: ~500ms delay (with jitter)
- Attempt 3: ~1000ms delay (exponential backoff with jitter)
- Timeout: 30 seconds per request

**Files Added**:
- `src/Rag.Api/Configuration/ResilienceConfiguration.cs`

**Files Modified**:
- `src/Rag.Infrastructure/Claude/ClaudeChatModel.cs` - Uses `IHttpClientFactory`
- `src/Rag.Infrastructure/OpenAI/OpenAiEmbeddingModel.cs` - Uses `IHttpClientFactory`
- `src/Rag.Infrastructure/Qdrant/QdrantVectorStore.cs` - Uses `IHttpClientFactory`
- `src/Rag.Core/Models/Settings.cs` - Added `ResilienceSettings`

---

### 3️⃣ **API Key Protection**

**Purpose**: Simple header-based internal API key for access control.

**Implementation**:
- Custom middleware validates `X-API-Key` header on all protected endpoints
- Bypasses authentication for:
  - `/` (root health check)
  - `/swagger` (API documentation)
  - `/health` (future health endpoint)
- Returns `401 Unauthorized` if:
  - API key is missing
  - API key is invalid
- **Development Mode**: If `Security:ApiKey` is empty/not configured, authentication is **disabled** with a warning log

**Configuration** (`appsettings.json`):
```json
"Security": {
  "ApiKey": "your-secret-api-key-here"
}
```

**Usage Example**:
```bash
# Without API key (will fail in production)
curl http://localhost:5129/ask

# With API key
curl -H "X-API-Key: your-secret-api-key-here" \
     -H "Content-Type: application/json" \
     -d '{"question":"What is RAG?","topK":5}' \
     http://localhost:5129/ask
```

**Files Added**:
- `src/Rag.Api/Middleware/ApiKeyAuthMiddleware.cs`
- `src/Rag.Core/Models/Settings.cs` - Added `SecuritySettings`

**Files Modified**:
- `src/Rag.Api/Program.cs` - Added API key middleware

---

## 🎯 Testing the Implementation

### Test Rate Limiting:
```bash
# Spam requests to trigger rate limit
for i in {1..35}; do
  curl -H "X-API-Key: your-key" \
       -H "Content-Type: application/json" \
       -d '{"question":"test","topK":3}' \
       http://localhost:5129/ask
done
```

Expected: First 30 succeed, remaining return `429 Too Many Requests`

### Test Retry Policy:
1. Stop Qdrant: `docker stop qdrant`
2. Make request to `/ask` - should retry 3 times with exponential backoff
3. Check logs for retry attempts
4. Restart Qdrant: `docker start qdrant`

### Test API Key Protection:
```bash
# Should fail with 401
curl http://localhost:5129/ask

# Should succeed
curl -H "X-API-Key: your-key" http://localhost:5129/ask
```

---

## 📊 Monitoring & Observability

**Logs to Watch**:
- **Retry attempts**: `{ServiceName} request failed (Attempt {Attempt}/{MaxAttempts})`
- **Rate limiting**: `429 Too Many Requests` responses
- **API key failures**: `Missing API key from {IP}` or `Invalid API key attempt from {IP}`

**Future Enhancements** (Phase 2):
- Structured logging with Serilog
- OpenTelemetry tracing
- Prometheus metrics for:
  - Request rate
  - Retry counts
  - Token usage
  - Cache hit rates

---

## 🔒 Security Best Practices

1. **Never commit API keys** to source control
   - Use environment variables in production
   - Use Azure Key Vault or similar secret management

2. **Set strong API key in production**:
   ```bash
   export Security__ApiKey="$(openssl rand -base64 32)"
   ```

3. **Enable HTTPS** in production (already configured in ASP.NET Core)

4. **Monitor rate limit logs** for potential abuse patterns

5. **Consider IP-based rate limiting** for additional protection (future enhancement)

---

## 🚀 Deployment Checklist

- [ ] Set `Security:ApiKey` in production configuration
- [ ] Configure `RateLimiting` values based on expected load
- [ ] Adjust `Resilience:TimeoutSeconds` for production network latency
- [ ] Enable structured logging
- [ ] Set up monitoring/alerting for 429 and 401 responses
- [ ] Test rate limiting under production-like load
- [ ] Verify retry behavior with simulated failures

---

## 📦 NuGet Packages Added

```xml
<PackageReference Include="Microsoft.AspNetCore.RateLimiting" Version="10.0.2" />
<PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="10.0.2" />
```

---

## 🎓 Architecture Decisions

### Why Fixed Window Rate Limiting?
- Simple and deterministic
- Low memory overhead
- Sufficient for preventing abuse
- Can upgrade to sliding window or token bucket if needed

### Why Polly v8 with Microsoft.Extensions.Http.Resilience?
- Native integration with `IHttpClientFactory`
- Modern async/await patterns
- Built-in support for exponential backoff with jitter
- Telemetry-friendly for OpenTelemetry

### Why Middleware for API Key?
- Centralized authentication logic
- Easy to bypass specific endpoints
- Simple to extend with more sophisticated auth (JWT, OAuth2)
- Logging at the entry point

---

## 🔜 Next Steps (Phase 2)

See the main README for Phase 2 enhancements:
- [ ] Distributed caching (Redis)
- [ ] Enhanced observability (OpenTelemetry, Serilog)
- [ ] Token usage logging
- [ ] Stronger prompt injection detection
- [ ] Policy-based authorization

---

**Status**: ✅ Phase 1 Complete - Production-Ready Hardening Implemented
