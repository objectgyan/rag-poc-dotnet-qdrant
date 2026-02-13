# 🚀 Multi-Tenancy Quick Start Guide

## TL;DR

Your RAG API now supports **enterprise multi-tenancy** with complete tenant data isolation.

---

## ✨ What Changed?

### Before (Single-Tenant)
```bash
curl -X POST http://localhost:5129/ask \
  -H "X-API-Key: key" \
  -d '{"question":"What is RAG?"}'
```

### After (Multi-Tenant)
```bash
curl -X POST http://localhost:5129/ask \
  -H "X-API-Key: key" \
  -H "X-Tenant-Id: acme-corp" \
  -d '{"question":"What is RAG?"}'
```

✅ **New Header**: `X-Tenant-Id`  
✅ **Automatic Filtering**: Only returns data for specified tenant  
✅ **Response Includes**: `tenantId` field  

---

## 🎯 Quick Examples

### 1. Ingest for Tenant A
```bash
curl -X POST http://localhost:5129/ingest \
  -H "X-API-Key: secure_password" \
  -H "X-Tenant-Id: tenant-a" \
  -H "Content-Type: application/json" \
  -d '{
    "documentId": "doc-a1",
    "text": "Tenant A confidential information"
  }'
```

**Response**:
```json
{
  "documentId": "doc-a1",
  "chunkCount": 1,
  "tenantId": "tenant-a"
}
```

---

### 2. Ingest for Tenant B
```bash
curl -X POST http://localhost:5129/ingest \
  -H "X-API-Key: secure_password" \
  -H "X-Tenant-Id: tenant-b" \
  -H "Content-Type: application/json" \
  -d '{
    "documentId": "doc-b1",
    "text": "Tenant B confidential information"
  }'
```

**Response**:
```json
{
  "documentId": "doc-b1",
  "chunkCount": 1,
  "tenantId": "tenant-b"
}
```

---

### 3. Query as Tenant A (Isolated)
```bash
curl -X POST http://localhost:5129/ask \
  -H "X-API-Key: secure_password" \
  -H "X-Tenant-Id: tenant-a" \
  -H "Content-Type: application/json" \
  -d '{
    "question": "What documents do I have?",
    "topK": 10
  }'
```

**Response**:
```json
{
  "answer": "You have doc-a1 with confidential information for Tenant A.",
  "citations": [
    {
      "documentId": "doc-a1",
      "chunkIndex": 0,
      "score": 0.95
    }
  ],
  "tenantId": "tenant-a"
}
```

✅ **Tenant A only sees their own data** - Tenant B's documents are filtered out.

---

### 4. Query as Tenant B (Isolated)
```bash
curl -X POST http://localhost:5129/ask \
  -H "X-API-Key: secure_password" \
  -H "X-Tenant-Id: tenant-b" \
  -H "Content-Type: application/json" \
  -d '{
    "question": "What documents do I have?",
    "topK": 10
  }'
```

**Response**:
```json
{
  "answer": "You have doc-b1 with confidential information for Tenant B.",
  "citations": [
    {
      "documentId": "doc-b1",
      "chunkIndex": 0,
      "score": 0.95
    }
  ],
  "tenantId": "tenant-b"
}
```

✅ **Complete isolation** - No cross-tenant data leakage.

---

## ⚙️ Configuration Modes

### Mode 1: Development (Flexible)

**appsettings.Development.json**:
```json
{
  "MultiTenancy": {
    "Enabled": true,
    "RequireTenantId": false,
    "DefaultTenantId": "dev-tenant"
  }
}
```

**Behavior**:
- Tenant header is **optional**
- Uses `dev-tenant` if header not provided
- Good for local testing

**Example** (no header):
```bash
curl -X POST http://localhost:5129/ask \
  -H "X-API-Key: secure_password" \
  -d '{"question":"test"}'
```
→ Uses `dev-tenant` automatically

---

### Mode 2: Production (Strict)

**appsettings.Production.json**:
```json
{
  "MultiTenancy": {
    "Enabled": true,
    "RequireTenantId": true,
    "DefaultTenantId": null
  }
}
```

**Behavior**:
- Tenant header is **required**
- Returns `400` if header missing
- Maximum security

**Example** (no header):
```bash
curl -X POST http://localhost:5129/ask \
  -H "X-API-Key: secure_password" \
  -d '{"question":"test"}'
```

**Error Response**:
```json
{
  "error": "missing_tenant_id",
  "message": "Multi-tenancy is enabled. Provide tenant ID via 'X-Tenant-Id' header."
}
```

---

## 🔒 Tenant ID Validation

**Valid Tenant IDs**:
- `tenant-a`
- `acme-corp`
- `client_123`
- `org-456-test`

**Invalid Tenant IDs**:
- `tenant@acme` ❌ (no special chars)
- `tenant#123` ❌ (only alphanumeric, hyphens, underscores)
- `tenant with spaces` ❌ (no spaces)

**Invalid Request**:
```bash
curl -X POST http://localhost:5129/ask \
  -H "X-Tenant-Id: invalid@tenant!" \
  -H "X-API-Key: secure_password" \
  -d '{"question":"test"}'
```

**Error**:
```json
{
  "error": "invalid_tenant_id",
  "message": "Tenant ID must contain only alphanumeric characters, hyphens, and underscores."
}
```

---

## 📊 What Happens Under the Hood?

### 1. **Ingestion** (with tenant)

```
Document → Chunks → Embeddings → Qdrant
                                    │
                                    ▼
                        Payload includes: tenantId
```

**Qdrant Payload**:
```json
{
  "documentId": "doc-001",
  "chunkIndex": 0,
  "text": "Sample text",
  "tenantId": "acme-corp"  ← Added automatically
}
```

---

### 2. **Query** (with tenant filtering)

```
Question → Embedding → Qdrant Search
                            │
                            ▼
                     Filter: tenantId = "acme-corp"
                            │
                            ▼
                     Only tenant's documents returned
```

**Qdrant Filter** (automatically added):
```json
{
  "filter": {
    "must": [
      {
        "key": "tenantId",
        "match": { "value": "acme-corp" }
      }
    ]
  }
}
```

---

## 🧪 Testing Multi-Tenancy

### Test Script

```bash
#!/bin/bash

API_KEY="secure_password"
BASE_URL="http://localhost:5129"

echo "=== Ingest for Tenant A ==="
curl -X POST "$BASE_URL/ingest" \
  -H "X-API-Key: $API_KEY" \
  -H "X-Tenant-Id: tenant-a" \
  -H "Content-Type: application/json" \
  -d '{"documentId":"ta-doc","text":"Tenant A secret data"}'

echo -e "\n\n=== Ingest for Tenant B ==="
curl -X POST "$BASE_URL/ingest" \
  -H "X-API-Key: $API_KEY" \
  -H "X-Tenant-Id: tenant-b" \
  -H "Content-Type: application/json" \
  -d '{"documentId":"tb-doc","text":"Tenant B secret data"}'

echo -e "\n\n=== Query as Tenant A ==="
curl -X POST "$BASE_URL/ask" \
  -H "X-API-Key: $API_KEY" \
  -H "X-Tenant-Id: tenant-a" \
  -H "Content-Type: application/json" \
  -d '{"question":"What secret data do you have?","topK":10}'

echo -e "\n\n=== Query as Tenant B ==="
curl -X POST "$BASE_URL/ask" \
  -H "X-API-Key: $API_KEY" \
  -H "X-Tenant-Id: tenant-b" \
  -H "Content-Type: application/json" \
  -d '{"question":"What secret data do you have?","topK":10}'
```

**Expected Result**:
- Tenant A sees only "Tenant A secret data"
- Tenant B sees only "Tenant B secret data"
- ✅ Complete isolation verified

---

## 📚 Full Documentation

For complete details, see:
- **[PHASE2-MULTI-TENANCY.md](PHASE2-MULTI-TENANCY.md)** - Full architecture & implementation details
- **[API-USAGE-EXAMPLES.md](API-USAGE-EXAMPLES.md)** - Updated with multi-tenancy examples

---

## 🎉 Summary

✅ **Multi-tenancy enabled** - Add `X-Tenant-Id` header  
✅ **Automatic filtering** - Qdrant filters by tenant  
✅ **Configurable** - Strict or flexible mode  
✅ **Validated** - Tenant ID format validation  
✅ **Auditable** - All logs include tenant ID  

**Next**: Test with multiple tenants and verify data isolation! 🚀
