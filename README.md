# AI RAG Platform (.NET + Qdrant + Claude + OpenAI)

## Overview

This project is a production-ready Retrieval-Augmented Generation (RAG) API built using:

* **.NET 10 Web API**
* **OpenAI Embeddings**
* **Qdrant Vector Database**
* **Claude (Anthropic) for generation**

It is designed with clean architecture principles and built to support:

* Multi-provider LLM integration
* Citation-based responses
* Prompt injection protection
* Embedding caching
* Enterprise extensibility

---

## Why This Exists

Large Language Models hallucinate.

This system ensures:

* Responses are grounded in indexed documents
* Answers include verifiable citations
* Context injection is controlled and sanitized
* Architecture is scalable and extensible

---

# 🏗 Architecture Overview

```
Client (Postman / Frontend)
        │
        ▼
Rag.Api (.NET Web API)
        │
        ├── IEmbeddingModel (OpenAI)
        ├── IVectorStore (Qdrant)
        └── IChatModel (Claude)
                │
                ▼
           Final Answer
```

---

## Layered Structure

### Rag.Core

* Abstractions (interfaces)
* Domain models
* Text chunking
* Prompt guards

### Rag.Infrastructure

* OpenAI embedding implementation
* Claude chat implementation
* Qdrant vector store implementation
* Caching decorator

### Rag.Api

* Controllers
* Dependency injection
* Error handling
* Observability

---

# 🔁 Data Flow

## Ingestion Flow

1. Document received via `/ingest`
2. Text is chunked
3. Each chunk is embedded using OpenAI
4. Embeddings stored in Qdrant with metadata
5. Stable UUID prevents duplication

---

## Query Flow

1. User sends question to `/ask`
2. Question embedded
3. Qdrant similarity search retrieves relevant chunks
4. Context sanitized (prompt guard)
5. Claude generates grounded response
6. Citations returned with answer

---

# 🔐 Security Measures

* Prompt injection guard
* Stable UUID vector IDs
* Deterministic deduplication
* Structured error handling
* Context isolation design-ready

Planned:

* Tenant filtering
* API authentication
* Rate limiting
* Cost tracking

---

# ⚡ Performance Optimizations

* SHA256-based embedding cache
* Vector deduplication
* Minimal chunk overlap
* Controlled TopK retrieval

---

# 🧠 Key Design Decisions

### Why Qdrant?

* High-performance vector search
* Metadata filtering
* Lightweight local dev setup

### Why Separate Embedding + Chat Providers?

* Model flexibility
* Avoid vendor lock-in
* Cost optimization strategy

### Why Clean Architecture?

* Testability
* Replaceable providers
* Enterprise scalability

---

# 📈 Future Roadmap

* Multi-tenant support
* PDF ingestion
* Background ingestion jobs
* Rate limiting & cost protection
* RAG evaluation harness
* Agent layer integration

---

# 🎯 Positioning

This project demonstrates:

* Distributed AI system design
* LLM integration patterns
* Vector database engineering
* Security-aware prompt design
* Production-grade API architecture

---

# 👨‍💻 Author

Mayank Singh
Cloud & AI Systems Architect
Designing Secure, Scalable Enterprise Systems