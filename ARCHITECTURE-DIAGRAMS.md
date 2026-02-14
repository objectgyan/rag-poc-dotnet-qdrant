# Architecture Diagrams

Visual representation of the RAG POC system architecture using Mermaid diagrams.

## Viewing Options

### Mermaid Rendering Support

The diagrams in this document use Mermaid syntax, which is natively supported by:

- ✅ **GitHub** - Renders automatically in README and markdown files
- ✅ **VS Code** - With Mermaid extensions (Mermaid Preview, Markdown Preview Mermaid Support)
- ✅ **GitLab** - Native support
- ✅ **Azure DevOps** - With extensions
- ✅ **Notion, Confluence** - Via plugins

### Generating Static Images

If you need static PNG/SVG images instead:

#### Option 1: VS Code (Recommended)
1. Install **Mermaid Preview** extension
2. Right-click on any diagram → **Mermaid: Preview Diagram**
3. Click **Export** → Choose PNG or SVG

#### Option 2: Mermaid Live Editor
1. Visit https://mermaid.live/
2. Copy diagram code from this file
3. Click **Export** → Choose format (PNG, SVG, PDF)

#### Option 3: Command Line (mermaid-cli)
```bash
npm install -g @mermaid-js/mermaid-cli
mmdc -i ARCHITECTURE-DIAGRAMS.md -o diagrams/
```

#### Option 4: Automated in CI/CD
```yaml
# GitHub Actions example
- uses: neenjaw/compile-mermaid-markdown-action@master
  with:
    files: 'ARCHITECTURE-DIAGRAMS.md'
    output: 'docs/images'
```

## Table of Contents

1. [System Architecture](#system-architecture)
2. [Authentication Flow](#authentication-flow)
3. [RAG Query Flow](#rag-query-flow)
4. [Agent Workflow](#agent-workflow)
5. [Multi-Tenant Data Flow](#multi-tenant-data-flow)
6. [Component Dependencies](#component-dependencies)
7. [Document Ingestion Pipeline](#document-ingestion-pipeline)
8. [Deployment Architecture](#deployment-architecture)

---

## System Architecture

High-level view of all system components and their interactions.

```mermaid
graph TB
    subgraph "Frontend Layer"
        UI[Rag.Web<br/>React + TypeScript]
        Login[Login Component]
        Chat[Chat Interface]
        Sidebar[Sidebar + Navigation]
    end

    subgraph "API Layer"
        API[Rag.Api<br/>.NET 10]
        Auth[Authentication<br/>Controller]
        Ingest[Ingest<br/>Controller]
        Ask[Ask<br/>Controller]
        Agent[Agent<br/>Controller]
        Eval[Evaluation<br/>Controller]
    end

    subgraph "Core Layer"
        Abstractions[Abstractions<br/>Interfaces]
        Models[Domain Models]
        AgentCore[Agent Abstractions]
        TextProc[Text Processing<br/>Chunking]
    end

    subgraph "Infrastructure Layer"
        JWT[JWT Service]
        AuthSvc[Auth Service]
        OpenAI[OpenAI<br/>Embeddings]
        Claude[Claude<br/>Chat Model]
        Qdrant[Qdrant<br/>Vector Store]
        AgentOrch[Agent<br/>Orchestrator]
        Tools[Tool Registry<br/>+ Tools]
        EvalSvc[Evaluation<br/>Services]
    end

    subgraph "External Services"
        OpenAIAPI[OpenAI API]
        ClaudeAPI[Claude API]
        QdrantDB[(Qdrant DB)]
        GitHub[GitHub API]
    end

    UI --> Login
    UI --> Chat
    UI --> Sidebar
    
    Login -->|JWT Token| Auth
    Chat -->|HTTP/REST| API
    Sidebar -->|Workspace Switch| API
    
    Auth --> JWT
    Auth --> AuthSvc
    Ingest --> TextProc
    Ask --> OpenAI
    Ask --> Claude
    Ask --> Qdrant
    Agent --> AgentOrch
    Eval --> EvalSvc
    
    JWT --> Abstractions
    OpenAI --> OpenAIAPI
    Claude --> ClaudeAPI
    Qdrant --> QdrantDB
    AgentOrch --> Tools
    Tools --> GitHub
    Tools --> Qdrant
    
    style UI fill:#60a5fa
    style API fill:#34d399
    style Abstractions fill:#fbbf24
    style JWT fill:#a78bfa
```

---

## Authentication Flow

JWT-based password authentication with multi-tenant user mapping.

```mermaid
sequenceDiagram
    participant User
    participant Login as Login UI
    participant API as Auth Controller
    participant AuthSvc as Auth Service
    participant JWT as JWT Service
    participant Store as Zustand Store
    participant APIClient as API Client

    User->>Login: Enter credentials
    Login->>API: POST /authentication/login<br/>{username, password}
    API->>AuthSvc: AuthenticateAsync(username, password)
    
    alt Valid Credentials
        AuthSvc->>AuthSvc: Hash password<br/>Check against users
        AuthSvc-->>API: AuthenticationResult<br/>{userId, tenantId, role, tier}
        API->>JWT: GenerateToken(userId, tenantId, role, tier)
        JWT->>JWT: Create JWT with claims<br/>(sub, tenant_id, role, tier)
        JWT-->>API: JWT Token String
        API-->>Login: 200 OK<br/>{token, user, tenant}
        Login->>Store: login(token, user, tenant)
        Store->>APIClient: setToken(token)<br/>setTenantId(tenantId)
        Store->>Store: Persist to localStorage
        Login->>User: Navigate to Chat
    else Invalid Credentials
        AuthSvc-->>API: null
        API-->>Login: 401 Unauthorized<br/>{error: "Invalid credentials"}
        Login->>User: Show error message
    end

    Note over User,APIClient: Subsequent requests include<br/>Authorization: Bearer {token}
```

---

## RAG Query Flow

Document retrieval and answer generation pipeline.

```mermaid
sequenceDiagram
    participant User
    participant Chat as Chat Interface
    participant API as Ask Controller
    participant Embed as Embedding Model
    participant Qdrant as Vector Store
    participant Claude as Chat Model
    participant Cost as Cost Calculator

    User->>Chat: Enter question
    Chat->>API: POST /ask<br/>{question, topK}
    
    Note over API: Middleware validates JWT<br/>Extracts tenant_id
    
    API->>Embed: GenerateEmbedding(question)
    Embed->>Embed: Call OpenAI API<br/>text-embedding-3-small
    Embed-->>API: float[] embeddings
    
    API->>Qdrant: SearchAsync(embeddings, topK)<br/>Filter by tenant_id
    Qdrant->>Qdrant: Semantic similarity search<br/>Cosine distance
    Qdrant-->>API: List<SearchResult><br/>{documentId, score, text}
    
    API->>API: Build context from<br/>top-K chunks
    API->>Claude: AnswerAsync(question, context)
    Claude->>Claude: Call Claude API<br/>claude-sonnet-4-20250514
    Claude-->>API: Answer text
    
    API->>Cost: Calculate costs<br/>(embedding + chat tokens)
    Cost-->>API: CostInfo
    
    API-->>Chat: 200 OK<br/>{answer, citations, cost}
    Chat->>User: Display answer<br/>with citations
    
    Note over User,Cost: Cost: ~$0.0001-0.001 per query
```

---

## Agent Workflow

Multi-step agent reasoning with tool calling capabilities.

```mermaid
flowchart TD
    Start([User sends message]) --> ValidateJWT[Validate JWT<br/>Extract tenant_id]
    ValidateJWT --> InitAgent[Initialize Agent<br/>Orchestrator]
    InitAgent --> BuildHistory[Build conversation<br/>history]
    BuildHistory --> CallClaude1[Call Claude API<br/>with tool definitions]
    
    CallClaude1 --> Decision{Response type?}
    
    Decision -->|Text Response| FinalAnswer[Extract final answer]
    Decision -->|Tool Calls| ParseTools[Parse tool calls]
    
    ParseTools --> ExecuteTools[Execute tools in parallel]
    
    subgraph "Tool Execution"
        RagSearch[RAG Search Tool<br/>Search documents]
        GitHubRepos[GitHub Repos Tool<br/>Search repositories]
        GitHubCode[GitHub Code Tool<br/>Search code across GitHub]
    end
    
    ExecuteTools --> RagSearch
    ExecuteTools --> GitHubRepos
    ExecuteTools --> GitHubCode
    
    RagSearch --> Results1[Search results]
    GitHubRepos --> Results1
    GitHubCode --> Results1
    
    Results1 --> AppendToHistory[Append tool results<br/>to history]
    AppendToHistory --> CheckIterations{Max iterations<br/>reached?}
    
    CheckIterations -->|No| CallClaude2[Call Claude again<br/>with tool results]
    CallClaude2 --> Decision
    
    CheckIterations -->|Yes| MaxReached[Return partial answer]
    
    FinalAnswer --> CalculateCost[Calculate total cost<br/>and metrics]
    MaxReached --> CalculateCost
    
    CalculateCost --> Response["Return AgentChatResponse with answer, toolCalls, metrics"]
    Response --> End([Display to user])
    
    style Start fill:#60a5fa
    style End fill:#34d399
    style Decision fill:#fbbf24
    style CheckIterations fill:#fbbf24
```

---

## Multi-Tenant Data Flow

Tenant isolation and data segregation.

```mermaid
graph TB
    subgraph "User A - Tenant: tenant-mayank"
        UserA[Mayank]
        TokenA[JWT Token<br/>tenant_id: tenant-mayank]
    end
    
    subgraph "User B - Tenant: tenant-john"
        UserB[John]
        TokenB[JWT Token<br/>tenant_id: tenant-john]
    end
    
    subgraph "API Middleware"
        JWTMiddleware[JWT Auth Middleware]
        TenantContext[Tenant Context<br/>Scoped Service]
    end
    
    subgraph "Controllers"
        AskCtrl[Ask Controller]
        IngestCtrl[Ingest Controller]
        AgentCtrl[Agent Controller]
    end
    
    subgraph "Vector Store - Qdrant"
        Collection[(rag_chunks collection)]
        
        subgraph "Documents"
            DocA1[Document<br/>tenant_id: tenant-mayank<br/>doc: resume.pdf]
            DocA2[Document<br/>tenant_id: tenant-mayank<br/>doc: project.pdf]
            DocB1[Document<br/>tenant_id: tenant-john<br/>doc: notes.pdf]
        end
    end
    
    UserA -->|Bearer Token A| JWTMiddleware
    UserB -->|Bearer Token B| JWTMiddleware
    
    JWTMiddleware -->|Extract tenant_id| TenantContext
    TenantContext --> AskCtrl
    TenantContext --> IngestCtrl
    TenantContext --> AgentCtrl
    
    AskCtrl -->|Filter by tenant_id| Collection
    IngestCtrl -->|Add tenant_id to chunks| Collection
    AgentCtrl -->|Filter by tenant_id| Collection
    
    Collection --> DocA1
    Collection --> DocA2
    Collection --> DocB1
    
    DocA1 -.->|Visible to| UserA
    DocA2 -.->|Visible to| UserA
    DocB1 -.->|Visible to| UserB
    
    DocA1 -.->|Hidden from| UserB
    DocB1 -.->|Hidden from| UserA
    
    style UserA fill:#60a5fa
    style UserB fill:#34d399
    style TenantContext fill:#fbbf24
    style DocA1 fill:#dbeafe
    style DocA2 fill:#dbeafe
    style DocB1 fill:#d1fae5
```

---

## Component Dependencies

.NET project dependencies and layers.

```mermaid
graph BT
    subgraph "Presentation Layer"
        RagWeb[Rag.Web<br/>React Frontend]
        RagApi[Rag.Api<br/>ASP.NET Core]
    end
    
    subgraph "Domain Layer"
        RagCore[Rag.Core<br/>Interfaces + Models]
    end
    
    subgraph "Infrastructure Layer"
        RagInfra[Rag.Infrastructure<br/>Implementations]
    end
    
    subgraph "Test Layer"
        RagTests[Rag.Tests<br/>Integration Tests]
    end
    
    subgraph "External Dependencies"
        OpenAI[OpenAI SDK]
        Anthropic[Anthropic SDK]
        Qdrant[Qdrant Client]
        Hangfire[Hangfire]
    end
    
    RagWeb -->|HTTP/REST| RagApi
    RagApi --> RagCore
    RagApi --> RagInfra
    RagInfra --> RagCore
    RagTests --> RagApi
    RagTests --> RagInfra
    
    RagInfra --> OpenAI
    RagInfra --> Anthropic
    RagInfra --> Qdrant
    RagApi --> Hangfire
    
    style RagCore fill:#fbbf24
    style RagApi fill:#34d399
    style RagInfra fill:#60a5fa
    style RagWeb fill:#a78bfa
```

---

## Document Ingestion Pipeline

Background job processing for PDF and text ingestion.

```mermaid
stateDiagram-v2
    [*] --> UploadFile: User uploads file
    
    state "Ingest Controller" as Controller {
        UploadFile --> ValidateFile: Check file type
        ValidateFile --> CreateJob: Enqueue background job
    }
    
    state "Hangfire Background Job" as Hangfire {
        CreateJob --> ExtractText: PDF or Text extraction
        ExtractText --> Chunk: Split into chunks<br/>(512 tokens, 20% overlap)
        Chunk --> GenerateEmbeddings: OpenAI embeddings<br/>text-embedding-3-small
        GenerateEmbeddings --> AddMetadata: Add tenant_id,<br/>documentId, chunkIndex
    }
    
    state "Vector Store" as Store {
        AddMetadata --> UpsertVectors: Store in Qdrant<br/>with metadata filter
        UpsertVectors --> IndexReady: Ready for search
    }
    
    IndexReady --> [*]: Document ingested
    
    state "Error Handling" as Error {
        ValidateFile --> ErrorInvalidFile: Invalid format
        ExtractText --> ErrorExtraction: Extraction failed
        GenerateEmbeddings --> ErrorEmbedding: API failure
        UpsertVectors --> ErrorStorage: Storage failure
    }
    
    ErrorInvalidFile --> [*]: Return error
    ErrorExtraction --> [*]: Return error
    ErrorEmbedding --> [*]: Return error
    ErrorStorage --> [*]: Return error
    
    note right of Chunk
        Chunking Strategy:
        - Max 512 tokens per chunk
        - 20% overlap between chunks
        - Preserve sentence boundaries
    end note
    
    note right of UpsertVectors
        Metadata stored:
        - tenant_id (for isolation)
        - documentId (for citations)
        - chunkIndex (for ordering)
        - pageNumber (if PDF)
    end note
```

---

## Deployment Architecture

Production deployment components and infrastructure.

```mermaid
graph TB
    subgraph "Client Tier"
        Browser[Web Browser]
    end
    
    subgraph "Frontend Tier - CDN/Static Hosting"
        CDN[CDN<br/>Azure Static Web Apps<br/>or Vercel]
        RagWeb[Rag.Web<br/>React SPA]
    end
    
    subgraph "API Tier - Azure App Service"
        AppService[Azure App Service<br/>.NET 10]
        RagApi[Rag.Api]
        
        subgraph "Background Jobs"
            Hangfire[Hangfire Server<br/>Document Processing]
        end
    end
    
    subgraph "Data Tier"
        QdrantCloud[(Qdrant Cloud<br/>Vector Database)]
        BlobStorage[(Azure Blob Storage<br/>PDF Files)]
        AppInsights[Application Insights<br/>Monitoring]
    end
    
    subgraph "External APIs"
        OpenAIAPI[OpenAI API<br/>Embeddings]
        ClaudeAPI[Claude API<br/>Chat Completion]
        GitHubAPI[GitHub API<br/>Code Search]
    end
    
    Browser -->|HTTPS| CDN
    CDN --> RagWeb
    RagWeb -->|REST API<br/>JWT Auth| AppService
    AppService --> RagApi
    RagApi --> Hangfire
    
    RagApi -->|Vector Search| QdrantCloud
    RagApi -->|File Storage| BlobStorage
    RagApi -->|Telemetry| AppInsights
    Hangfire -->|Upload Embeddings| QdrantCloud
    
    RagApi -->|Generate Embeddings| OpenAIAPI
    RagApi -->|Chat Completion| ClaudeAPI
    RagApi -->|Search Repos/Code| GitHubAPI
    
    style Browser fill:#60a5fa
    style CDN fill:#34d399
    style AppService fill:#fbbf24
    style QdrantCloud fill:#a78bfa
    style OpenAIAPI fill:#f87171
    style ClaudeAPI fill:#f87171
```

**Deployment Notes:**

- **Scalability**: Frontend on Edge CDN, API with horizontal scaling, Qdrant managed cloud, Background workers for jobs
- **Security**: JWT tokens (8h expiry), HTTPS only, Tenant isolation, API key rotation
- **Monitoring**: Application Insights, Cost tracking per query, Error logging, Performance metrics

---

## Technology Stack Summary

```mermaid
mindmap
  root((RAG POC))
    Frontend
      React 18
      TypeScript
      TailwindCSS
      Zustand
      Axios
      Vite
    Backend
      .NET 10
      ASP.NET Core
      Minimal APIs
      Entity Framework
      Hangfire
    AI/ML
      OpenAI
        text-embedding-3-small
      Anthropic
        claude-sonnet-4
      Qdrant
        Vector Database
    Infrastructure
      Azure
        App Service
        Static Web Apps
        Blob Storage
      Authentication
        JWT
        Password-based
      Monitoring
        Application Insights
        Cost Tracking
```

---

## Data Models

Entity relationships and data structures.

```mermaid
erDiagram
    USER ||--|| TENANT : "belongs to"
    USER {
        string userId PK
        string username
        string passwordHash
        string role
        string tier
    }
    
    TENANT ||--o{ DOCUMENT : "owns"
    TENANT {
        string tenantId PK
        string tenantName
        string color
    }
    
    DOCUMENT ||--o{ CHUNK : "contains"
    DOCUMENT {
        string documentId PK
        string tenantId FK
        string fileName
        datetime uploadedAt
        string status
    }
    
    CHUNK ||--|| VECTOR : "has"
    CHUNK {
        string chunkId PK
        string documentId FK
        string tenantId FK
        int chunkIndex
        int pageNumber
        string text
    }
    
    VECTOR {
        float[] embedding
        string chunkId FK
        float score
    }
    
    USER ||--o{ QUERY : "makes"
    QUERY {
        string queryId PK
        string userId FK
        string tenantId FK
        string question
        string answer
        float cost
        datetime timestamp
    }
    
    QUERY ||--o{ CITATION : "includes"
    CITATION {
        string citationId PK
        string queryId FK
        string chunkId FK
        float score
    }
```

---

## Performance Metrics

Key performance indicators and optimization targets.

```mermaid
graph LR
    subgraph "Latency Targets"
        L1[Authentication<br/>< 200ms]
        L2[RAG Query<br/>< 2s]
        L3[Agent Query<br/>< 10s]
        L4[Document Ingestion<br/>< 30s]
    end
    
    subgraph "Cost Targets"
        C1[Embedding<br/>$0.00001/query]
        C2[Chat Completion<br/>$0.0001/query]
        C3[Total per Query<br/>< $0.001]
    end
    
    subgraph "Quality Metrics"
        Q1[Answer Relevance<br/>> 0.8]
        Q2[Context Precision<br/>> 0.85]
        Q3[Faithfulness<br/>> 0.9]
        Q4[Hallucination Rate<br/>< 5%]
    end
    
    subgraph "Scalability"
        S1[Concurrent Users<br/>100+]
        S2[Documents per Tenant<br/>10,000+]
        S3[Queries per Minute<br/>1,000+]
    end
    
    style L1 fill:#34d399
    style L2 fill:#60a5fa
    style L3 fill:#fbbf24
    style C1 fill:#a78bfa
    style Q1 fill:#f87171
    style S1 fill:#fb923c
```

---

## Quick Reference

| Diagram | Purpose | Best For |
|---------|---------|----------|
| System Architecture | Overall system design | Understanding component relationships |
| Authentication Flow | Login process | Security implementation |
| RAG Query Flow | Document retrieval | RAG pipeline understanding |
| Agent Workflow | Multi-step reasoning | Agent behavior comprehension |
| Multi-Tenant Data Flow | Data isolation | Security & privacy verification |
| Component Dependencies | Project structure | Development & testing |
| Document Ingestion | Background processing | Data pipeline understanding |
| Deployment Architecture | Production setup | DevOps & deployment |

---

**Generated**: February 13, 2026  
**Version**: 1.0  
**Project**: RAG POC - Multi-Tenant AI Agent Platform
