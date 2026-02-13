# 🔐💰 PHASE 3: Cost Tracking & Advanced Authentication

## Overview

Phase 3 adds production-critical features for **cost monitoring**, **JWT authentication**, **role-based access control (RBAC)**, and **tier-based rate limiting**. These features are essential for:

- **Cost Optimization**: Track token usage and API costs per request/user/tenant
- **Billing & Chargebacks**: Accurate cost attribution for multi-tenant systems
- **Security**: JWT-based authentication with role and tier claims
- **Fair Usage**: Subscription-tier-based rate limiting (Free/Pro/Enterprise)
- **Monitoring**: Cost metrics and alerting for budget control

## 🎯 Features Implemented

### 1. JWT Authentication
- **Token Validation**: HS256-signed JWT tokens with issuer/audience validation
- **Claims Extraction**: User ID, tenant ID, role, and subscription tier
- **Fallback**: Graceful fallback to API key authentication when JWT is disabled
- **Token Generation**: Built-in token generator for testing

### 2. Cost Tracking
- **Token Usage Capture**: Automatic extraction from OpenAI and Claude API responses
- **Cost Calculation**: Model-specific pricing (per 1M tokens) for accurate cost estimates
- **Request Aggregation**: Combines embedding + chat costs per request
- **Cost Logging**: Detailed per-request cost summaries with user/tenant attribution
- **Threshold Alerts**: Configurable warnings when request costs exceed threshold

### 3. Role-Based Access Control
- **User Roles**: ReadOnly, User, Admin (hierarchical)
- **Context Injection**: Request-scoped `IUserContext` with role/tier information
- **Authorization**: Role-based access checks (future: use `[Authorize(Roles = "Admin")]`)

### 4. Tier-Based Rate Limiting
- **Subscription Tiers**: Free, Pro, Enterprise
- **Dynamic Limits**: Rate limits automatically adjust based on user's tier
- **Per-User Partitioning**: Rate limits are per-user, not global
- **Tier-Specific Response**: 429 errors include tier info and upgrade prompt

## 📊 Token Usage & Cost Model

### Supported Models & Pricing (per 1M tokens)

| Model | Provider | Input Cost | Output Cost |
|-------|----------|-----------|-------------|
| text-embedding-3-small | OpenAI | $0.02 | - |
| text-embedding-3-large | OpenAI | $0.13 | - |
| gpt-4o | OpenAI | $2.50 | $10.00 |
| gpt-4o-mini | OpenAI | $0.15 | $0.60 |
| claude-sonnet-4-20250514 | Anthropic | $3.00 | $15.00 |
| claude-3-5-haiku-latest | Anthropic | $1.00 | $5.00 |
| claude-3-opus-latest | Anthropic | $15.00 | $75.00 |

### Cost Calculation Example

**Scenario**: User asks a question, triggering:
1. **Embedding**: 100 input tokens with `text-embedding-3-small`
2. **Chat**: 500 input + 200 output tokens with `claude-sonnet-4`

**Calculation**:
```
Embedding cost = (100 / 1,000,000) × $0.02 = $0.000002
Chat input cost = (500 / 1,000,000) × $3.00 = $0.0015
Chat output cost = (200 / 1,000,000) × $15.00 = $0.003
Total cost = $0.004502 (~0.45 cents)
```

## 🎫 JWT Token Structure

### Token Claims

```json
{
  "sub": "user-123",           // User ID (required)
  "tenant_id": "acme-corp",    // Tenant ID (optional)
  "role": "User",              // UserRole enum: ReadOnly, User, Admin
  "tier": "Pro",               // SubscriptionTier: Free, Pro, Enterprise
  "iss": "RagPocApi",
  "aud": "RagPocClient",
  "exp": 1746041234,
  "iat": 1746037634
}
```

### Generating Test Tokens (Development)

You can use the built-in JWT service to generate tokens for testing:

```csharp
var jwtService = app.Services.GetRequiredService<IJwtService>();

var token = jwtService.GenerateToken(
    userId: "test-user",
    tenantId: "test-tenant",
    role: UserRole.Admin,
    tier: SubscriptionTier.Enterprise,
    expirationMinutes: 60
);

Console.WriteLine($"JWT Token: {token}");
```

Or use an online JWT generator (e.g., jwt.io) with:
- Algorithm: HS256
- Secret: Value from `appsettings.Development.json` → `Jwt:SecretKey`
- Payload: Include `sub`, `tenant_id`, `role`, `tier` claims

## 🔧 Configuration

### appsettings.Development.json

```json
{
  "Jwt": {
    "Enabled": false,                    // Set to true to enable JWT auth
    "Issuer": "RagPocApi",
    "Audience": "RagPocClient",
    "SecretKey": "your-very-secure-secret-key-at-least-32-characters-long"
  },
  
  "CostTracking": {
    "Enabled": true,                     // Enable cost tracking
    "LogCostSummary": true,              // Log detailed cost per request
    "EmitMetrics": false,                // Future: Send to Prometheus/AppInsights
    "CostWarningThreshold": 0.10         // Warn if request cost > $0.10
  },
  
  "Security": {
    "ApiKey": "secure_password"          // Fallback when JWT disabled
  }
}
```

## 🎭 Subscription Tiers & Rate Limits

### Tier Definitions

| Tier | Ask Req/Min | Ingest Req/Min | Daily Requests | Max Cost/Day | Features |
|------|-------------|----------------|----------------|--------------|----------|
| **Free** | 5 | 2 | 100 | $1.00 | Basic access |
| **Pro** | 50 | 20 | 10,000 | $50.00 | Priority support |
| **Enterprise** | Unlimited | Unlimited | Unlimited | Unlimited | Custom models, SLA |

### How It Works

1. **JWT Contains Tier**: Token includes `"tier": "Pro"` claim
2. **Middleware Extracts**: `JwtAuthMiddleware` populates `UserContext.Tier`
3. **Rate Limiter Reads**: `RateLimitingConfiguration` uses `UserContext.TierLimits`
4. **Per-User Enforcement**: Rate limits partition by user ID + endpoint

### Rate Limit Response Example

```json
{
  "error": "rate_limit_exceeded",
  "message": "Rate limit exceeded for Free tier. Please try again later or upgrade your subscription.",
  "tier": "Free",
  "retryAfter": 45.2
}
```

## 🚀 API Usage Examples

### 1. Using JWT Authentication

```bash
# Obtain JWT token (from your auth service or generate for testing)
TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."

# Call API with JWT
curl -X POST http://localhost:5129/ask \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "question": "What is RAG?",
    "topK": 3
  }'
```

### 2. Using API Key (Fallback)

```bash
# Use API key header
curl -X POST http://localhost:5129/ask \
  -H "Content-Type: application/json" \
  -H "X-API-Key: secure_password" \
  -d '{
    "question": "What is RAG?",
    "topK": 3
  }'
```

### 3. Multi-Tenant Request with JWT

```bash
# JWT contains tenant_id claim, no need for X-Tenant-Id header
curl -X POST http://localhost:5129/ask \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "question": "Show customer data",
    "topK": 5
  }'

# Response includes tenant information
{
  "answer": "...",
  "citations": [...],
  "tenantId": "acme-corp"
}
```

## 📝 Cost Tracking Logs

When `CostTracking.LogCostSummary` is enabled, each request logs:

```
[Information] Cost Summary | User: test-user | Tenant: acme-corp | Tier: Pro | 
Endpoint: /ask | Status: 200 | Duration: 1245ms | 
Embedding Tokens: 150 | Chat Input: 650 | Chat Output: 220 | 
Total Tokens: 1020 | Cost: $0.004820
```

### Log Fields
- **User**: User ID from JWT or "anonymous"
- **Tenant**: Tenant ID from JWT/header or "default"
- **Tier**: Subscription tier (Free/Pro/Enterprise)
- **Endpoint**: Request path
- **Status**: HTTP status code
- **Duration**: Request processing time (ms)
- **Token Counts**: Breakdown by embedding/chat input/output
- **Cost**: Total estimated cost in USD

## ⚠️ Cost Threshold Warnings

If a single request exceeds `CostWarningThreshold` (default: $0.10):

```
[Warning] High Cost Alert | User: power-user | Tenant: bigcorp | Endpoint: /ask | 
Cost: $0.125000 exceeds threshold $0.100000
```

**Use Cases**:
- Detect expensive queries (e.g., large document ingestion)
- Alert on potential abuse or runaway costs
- Trigger investigation for optimization opportunities

## 🏗️ Architecture

### Middleware Pipeline Order

```
1. ExceptionHandler
2. JwtAuthMiddleware       ← Validates JWT, populates UserContext
3. TenantMiddleware        ← Extracts tenant from JWT or header
4. CostTrackingMiddleware  ← Aggregates costs, logs summary
5. RateLimiter             ← Tier-based rate limiting
6. Controllers             ← Tracks token usage per API call
```

### Request Flow (Ask Endpoint)

```
1. Request arrives with JWT token
2. JwtAuthMiddleware validates token, extracts user/role/tier
3. TenantMiddleware gets tenant ID from JWT claims
4. CostTrackingMiddleware starts request timer
5. RateLimiter checks user's tier limits (e.g., 50 req/min for Pro)
6. AskController:
   a. Calls embedding service → captures token usage
   b. Calls vector search
   c. Calls chat service → captures token usage
7. CostTrackingMiddleware calculates total cost, logs summary
8. Response returned with tenant ID
```

## 🔒 Security Best Practices

### Production Deployment

1. **Secure JWT Secret**: Use a cryptographically random key (min 32 chars)
   ```bash
   # Generate secure key
   openssl rand -base64 32
   ```

2. **Store in Secrets Manager**: Never commit secrets to Git
   - Azure: Azure Key Vault
   - AWS: AWS Secrets Manager
   - Local: User Secrets (`dotnet user-secrets`)

3. **Enable JWT Auth**: Set `Jwt.Enabled: true` in production
   
4. **Rotate Keys**: Periodically rotate JWT signing keys

5. **HTTPS Only**: Always use HTTPS in production

6. **Token Expiration**: Keep JWT expiration short (15-60 minutes)

## 📊 Future Enhancements

### Phase 3+: Advanced Features

- [ ] **Metrics Integration**: Send cost metrics to Prometheus/Application Insights
- [ ] **Daily Limit Enforcement**: Block users exceeding tier daily limits
- [ ] **Cost Budgets**: Per-user/tenant cost budgets with auto-cutoff
- [ ] **Billing Integration**: Export cost data for invoicing
- [ ] **Role-Based Authorization**: Use `[Authorize(Roles = "Admin")]` attributes
- [ ] **Audit Logging**: Track all user actions for compliance
- [ ] **Custom Pricing**: Per-tenant pricing overrides
- [ ] **Cost Forecasting**: Predict monthly costs based on usage trends

## 🧪 Testing Guide

### 1. Test JWT Authentication

```bash
# Generate a test token (use your IJwtService or jwt.io)
TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ0ZXN0LXVzZXIiLCJ0ZW5hbnRfaWQiOiJ0ZXN0LXRlbmFudCIsInJvbGUiOiJVc2VyIiwidGllciI6IkZyZWUiLCJleHAiOjk5OTk5OTk5OTl9.SIGNATURE"

# Test with valid JWT
curl -X POST http://localhost:5129/ask \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"question": "Test", "topK": 1}'

# Test with invalid JWT (should return 401)
curl -X POST http://localhost:5129/ask \
  -H "Authorization: Bearer invalid-token" \
  -H "Content-Type: application/json" \
  -d '{"question": "Test", "topK": 1}'
```

### 2. Test Tier-Based Rate Limiting

```bash
# Free tier user (5 req/min)
# Make 6 rapid requests - 6th should return 429
for i in {1..6}; do
  curl -X POST http://localhost:5129/ask \
    -H "Authorization: Bearer $FREE_TIER_TOKEN" \
    -H "Content-Type: application/json" \
    -d '{"question": "Test '$i'", "topK": 1}'
  echo ""
done

# Pro tier user (50 req/min) - should handle all requests
for i in {1..10}; do
  curl -X POST http://localhost:5129/ask \
    -H "Authorization: Bearer $PRO_TIER_TOKEN" \
    -H "Content-Type: application/json" \
    -d '{"question": "Test '$i'", "topK": 1}'
  echo ""
done
```

### 3. Test Cost Tracking

```bash
# Make a request and check logs for cost summary
curl -X POST http://localhost:5129/ask \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "question": "Explain machine learning",
    "topK": 5
  }'

# Check application logs for:
# [Information] Cost Summary | User: test-user | ... | Cost: $0.XXXXXX
```

## 📚 Code Examples

### Accessing User Context in Controllers

```csharp
[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IUserContext _userContext;

    public AdminController(IUserContext userContext)
    {
        _userContext = userContext;
    }

    [HttpGet("stats")]
    public IActionResult GetStats()
    {
        // Check role
        if (!_userContext.HasRole(UserRole.Admin))
        {
            return Forbid("Admin role required");
        }

        // Check tier
        if (!_userContext.HasTier(SubscriptionTier.Pro))
        {
            return Forbid("Pro tier or higher required");
        }

        var userId = _userContext.UserId;
        var tier = _userContext.Tier;
        
        return Ok(new { userId, tier, message = "Admin access granted" });
    }
}
```

### Manual Cost Calculation

```csharp
var costCalculator = serviceProvider.GetRequiredService<ICostCalculator>();

var tokenUsage = new TokenUsage
{
    Model = "claude-sonnet-4-20250514",
    InputTokens = 1000,
    OutputTokens = 500,
    TotalTokens = 1500
};

var cost = costCalculator.CalculateCost(tokenUsage);
Console.WriteLine($"Estimated cost: ${cost:F6}");
// Output: Estimated cost: $0.010500
```

## 🔍 Troubleshooting

### Issue: JWT Validation Fails

**Symptoms**: All JWT requests return 401 Unauthorized

**Solutions**:
1. Check `Jwt.Enabled: true` in configuration
2. Verify `Jwt.SecretKey` matches token signing key
3. Ensure token has `sub` claim (user ID)
4. Check token expiration (`exp` claim)
5. Verify issuer (`iss`) and audience (`aud`) match config

### Issue: Cost Tracking Not Logging

**Symptoms**: No "Cost Summary" logs appear

**Solutions**:
1. Check `CostTracking.Enabled: true`
2. Check `CostTracking.LogCostSummary: true`
3. Verify log level is Information or lower
4. Ensure requests are actually calling embedding/chat APIs

### Issue: Rate Limiting Too Strict

**Symptoms**: Users getting 429 errors on valid tier

**Solutions**:
1. Check user's JWT `tier` claim
2. Verify `UserContext.Tier` is populated correctly
3. Check `TierLimits` values (e.g., `TierLimits.Pro.AskRequestsPerMinute`)
4. Consider adjusting tier limits in `UserContext.cs`

## 📈 Monitoring & Metrics (Future)

When `CostTracking.EmitMetrics: true` is enabled, you can integrate with:

### Prometheus Metrics (Example)
```
# HELP rag_api_token_usage_total Total tokens consumed
# TYPE rag_api_token_usage_total counter
rag_api_token_usage_total{user="user-123",tenant="acme",tier="Pro"} 15420

# HELP rag_api_cost_usd_total Total cost in USD
# TYPE rag_api_cost_usd_total counter
rag_api_cost_usd_total{user="user-123",tenant="acme",tier="Pro"} 0.0823
```

### Application Insights (Example)
```csharp
_telemetryClient.TrackMetric("TokenUsage", totalTokens, new Dictionary<string, string>
{
    { "UserId", userId },
    { "TenantId", tenantId },
    { "Tier", tier.ToString() },
    { "Model", model }
});

_telemetryClient.TrackMetric("ApiCost", totalCost, new Dictionary<string, string>
{
    { "UserId", userId },
    { "TenantId", tenantId }
});
```

## ✅ Summary

Phase 3 transforms your RAG API into a **production-grade, cost-conscious, enterprise-ready system**:

- ✅ **JWT Authentication** with role/tier claims
- ✅ **Cost Tracking** with per-request token usage and cost estimation
- ✅ **Tier-Based Rate Limiting** for fair usage across subscription tiers
- ✅ **User/Tenant Context** for authorization and attribution
- ✅ **Cost Alerting** for budget control and abuse detection
- ✅ **Detailed Logging** for billing, auditing, and optimization

**Next Steps**:
1. Enable JWT in production (`Jwt.Enabled: true`)
2. Integrate metrics with monitoring system (Prometheus/AppInsights)
3. Add role-based authorization to sensitive endpoints
4. Implement daily cost limits and budget enforcement
5. Export cost data for billing/invoicing
