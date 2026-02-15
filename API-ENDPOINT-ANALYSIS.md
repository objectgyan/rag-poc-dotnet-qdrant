# API Endpoint Usage Analysis

## ✅ ACTIVELY USED BY FRONTEND

These endpoints are currently being called by the React frontend:

### Authentication
- ✅ `POST /api/v1/authentication/login` - User login

### Agent (Primary Interface)
- ✅ `POST /api/v1/agent/chat` - Main chat interface with agent orchestration
- ✅ `GET /api/v1/agent/tools` - List available agent tools

### Document Management
- ✅ `POST /api/v1/ingest` - Ingest text documents
- ✅ `POST /api/v1/documents/upload-pdf` - Upload PDF files
- ✅ `DELETE /api/v1/documents/{documentId}` - Delete documents
- ✅ `PUT /api/v1/documents/{documentId}` - Update documents

---

## ⚠️ NOT USED BY FRONTEND (Candidates for Obsolete)

### Ask Controller (Superseded by Agent)
- ❌ `POST /api/v1/ask` - **Replaced by agent chat** (direct RAG, no tools)
- ❌ `GET /api/v1/ask/stream` - SSE streaming (not implemented in frontend)

### Agent Controller (Unused Features)
- ❌ `GET /api/v1/agent/tools/{toolName}` - Get specific tool (not needed)
- ❌ `POST /api/v1/agent/search-code` - Direct code search (agent uses internally)
- ❌ `POST /api/v1/agent/ingest-codebase` - Codebase ingestion (not exposed in UI)
- ❌ `GET /api/v1/agent/code-context` - Get code context (not used)

### Authentication (Unused)
- ❌ `GET /api/v1/authentication/validate` - Token validation (not needed)

### Memory (No Frontend Integration Yet)
- ❌ `POST /api/v1/memory` - Store memory
- ❌ `GET /api/v1/memory/search` - Search memories
- ❌ `GET /api/v1/memory` - Get all memories
- ❌ `GET /api/v1/memory/stats` - Memory statistics
- ❌ `DELETE /api/v1/memory/{memoryId}` - Delete specific memory
- ❌ `DELETE /api/v1/memory` - Clear all memories

### Cache (No Frontend Integration)
- ❌ `GET /api/v1/cache/stats` - Cache statistics
- ❌ `POST /api/v1/cache/clear` - Clear cache
- ❌ `GET /api/v1/cache/health` - Cache health check
- ❌ `GET /api/v1/cache/info` - Cache info

### Evaluation (No Frontend Integration)
- ❌ `POST /api/v1/evaluation/test-cases` - Create test case
- ❌ `GET /api/v1/evaluation/test-cases` - List test cases
- ❌ `GET /api/v1/evaluation/test-cases/{id}` - Get test case
- ❌ `PUT /api/v1/evaluation/test-cases/{id}` - Update test case
- ❌ `DELETE /api/v1/evaluation/test-cases/{id}` - Delete test case
- ❌ `POST /api/v1/evaluation/run` - Run evaluation
- ❌ `GET /api/v1/evaluation/runs/{runId}` - Get evaluation run
- ❌ `GET /api/v1/evaluation/runs` - List evaluation runs
- ❌ `GET /api/v1/evaluation/metrics` - Get metrics

---

## 🎯 RECOMMENDATION

### Mark as Obsolete (Deprecated)
These should be marked with `[Obsolete]` attribute:
1. **AskController** - Entire controller (POST /api/v1/ask, GET /api/v1/ask/stream)
   - Reason: Agent mode with `useRagForContext=true` provides same functionality plus tools

### Keep But Not Expose in UI
These are useful for direct API access/testing but not needed in frontend:
- Memory endpoints (useful for API consumers)
- Cache endpoints (admin/monitoring)
- Evaluation endpoints (testing/QA)
- Agent code search endpoints (internal tools)

### Add Frontend Integration (Future)
Consider adding UI for these valuable features:
- Memory management panel
- Cache statistics dashboard
- Evaluation test runner

---

## 📊 Usage Summary

- **Total Endpoints**: 35
- **Used by Frontend**: 7 (20%)
- **Unused by Frontend**: 28 (80%)
- **Should Deprecate**: 2 (AskController endpoints)
- **Keep for API Access**: 26

---

## 🔄 Migration Path

**For users currently using `/api/v1/ask`:**

```typescript
// OLD: Direct RAG
POST /api/v1/ask
{ question: "What is Qdrant?", topK: 5 }

// NEW: Agent with RAG
POST /api/v1/agent/chat
{
  message: "What is Qdrant?",
  config: { useRagForContext: true, maxToolCalls: 5 }
}
```

Benefits of migration:
- ✅ RAG search (same as before)
- ✅ Conversation memory
- ✅ GitHub search
- ✅ Code search
- ✅ Multi-step reasoning
- ✅ Tool orchestration
