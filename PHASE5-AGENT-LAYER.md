# 🤖 PHASE 5: Agent Layer - Tool-Calling Architecture

## Overview

Phase 5 transforms the RAG system into an **intelligent AI agent** capable of:

- **Tool Calling**: Dynamically invoke tools to answer complex questions
- **Multi-Step Reasoning**: Break down complex queries into tool calls
- **Codebase Understanding**: Ingest and query code repositories
- **GitHub Integration**: Search repositories and code on GitHub
- **MCP-Like Architecture**: Model Context Protocol-inspired tool abstraction

> **Why This Matters**: This phase elevates your RAG from a simple Q&A system to an **autonomous agent** that can research, plan, and execute multi-step tasks. This is cutting-edge AI engineering that sets you apart.

## 🎯 Features Implemented

### 1. MCP-Like Tool Abstraction

Inspired by **Model Context Protocol (MCP)**, tools are abstracted as:

```csharp
public interface ITool
{
    string Name { get; }
    string Description { get; }
    IReadOnlyList<ToolParameter> Parameters { get; }
    Task<ToolResult> ExecuteAsync(Dictionary<string, object> arguments, CancellationToken ct);
}
```

**Benefits**:
- Standardized tool interface
- Easy to add new tools
- Type-safe parameter validation
- Composable tool execution

### 2. Built-In Tools

#### RAG Search Tool (`rag_search`)
Searches ingested documents using semantic similarity.

**Parameters**:
- `query` (string, required): Search query
- `top_k` (number, optional): Number of results (default: 3)
- `tenant_id` (string, optional): Multi-tenancy isolation

**Example**:
```json
{
  "tool_name": "rag_search",
  "arguments": {
    "query": "What is Qdrant?",
    "top_k": 5
  }
}
```

#### GitHub Search Repositories (`github_search_repositories`)
Searches GitHub for repositories.

**Parameters**:
- `query` (string, required): Search query
- `sort` (string, optional): Sort by stars/forks/updated (default: stars)
- `max_results` (number, optional): Max results (default: 5)

#### GitHub Search Code (`github_search_code`)
Searches for code snippets on GitHub.

**Parameters**:
- `query` (string, required): Code search query
- `language` (string, optional): Programming language filter
- `max_results` (number, optional): Max results (default: 5)

### 3. Agent Orchestration

The **AgentOrchestrator** is the brain of the system:

1. **Receives user message**
2. **Analyzes** what tools are needed
3. **Plans** tool execution sequence
4. **Executes** tools (serially or in parallel)
5. **Synthesizes** final answer from tool results

**Capabilities**:
- Multi-step reasoning
- Parallel tool execution
- Conversation history tracking
- Chain-of-thought reasoning
- Cost tracking

### 4. Codebase Ingestion

Ingest and understand local codebases:

- **File Parsing**: Supports C#, Python, JS, TS, Java, Go, Ruby
- **Semantic Extraction**: Extracts classes, functions, methods
- **Chunking**: Smart code chunking with overlap
- **Indexing**: Stores code chunks in vector database

**Use Cases**:
- Ask questions about your codebase
- Find implementations of specific patterns
- Understand code architecture
- Navigate large codebases

### 5. Tool Registry

Centralized registry for tool management:

```csharp
public interface IToolRegistry
{
    void RegisterTool(ITool tool, ToolMetadata? metadata);
    ITool? GetTool(string name);
    IReadOnlyList<ITool> GetAllTools();
    IReadOnlyList<ITool> GetToolsByCategory(ToolCategory category);
    IReadOnlyList<ITool> SearchTools(string query);
}
```

**Features**:
- Thread-safe registration
- Category-based organization (RAG, GitHub, CodeAnalysis, etc.)
- Tag-based search
- Metadata tracking

## 📊 Architecture

### Agent Flow

```
User Question
    ↓
Agent Orchestrator
    ↓
┌─────────────────────────────────┐
│ Analyze Question                │
│ Determine Tools Needed          │
│ Plan Execution Strategy         │
└─────────────────────────────────┘
    ↓
┌─────────────────────────────────┐
│ Tool Execution                  │
│                                 │
│  ┌─────────┐  ┌──────────────┐ │
│  │ RAG     │  │ GitHub       │ │
│  │ Search  │  │ Search       │ │
│  └─────────┘  └──────────────┘ │
│       ↓              ↓          │
│   Results        Results        │
└─────────────────────────────────┘
    ↓
┌─────────────────────────────────┐
│ Synthesize Final Answer         │
│ Combine Tool Results            │
│ Generate Human-Readable Output  │
└─────────────────────────────────┘
    ↓
Final Answer to User
```

### Tool Call Format

Agents use JSON to specify tool calls:

```json
{
  "reasoning": "User wants to know about Qdrant. I should search the documentation.",
  "tool_calls": [
    {
      "tool_name": "rag_search",
      "arguments": {
        "query": "What is Qdrant?",
        "top_k": 3
      }
    }
  ]
}
```

## 🚀 API Endpoints

### Chat with Agent

**POST /agent/chat**

The primary agent endpoint. Processes natural language and orchestrates tool calls.

**Request**:
```json
{
  "message": "What is Qdrant and how does it compare to other vector databases? Check our docs and GitHub.",
  "conversationHistory": [
    {
      "role": "user",
      "content": "Previous question..."
    },
    {
      "role": "assistant",
      "content": "Previous answer..."
    }
  ],
  "config": {
    "maxToolCalls": 5,
    "allowParallelToolCalls": true,
    "useRagForContext": true,
    "topKDocuments": 3,
    "minRelevanceScore": 0.7,
    "enableChainOfThought": true,
    "systemPrompt": "You are a helpful AI assistant..."
  }
}
```

**Response**:
```json
{
  "answer": "Based on the documentation and GitHub research, Qdrant is a high-performance vector database...",
  "toolCalls": [
    {
      "toolName": "rag_search",
      "arguments": {
        "query": "What is Qdrant?",
        "top_k": 3
      },
      "reasoningTrace": "Searching internal documentation for Qdrant information"
    },
    {
      "toolName": "github_search_repositories",
      "arguments": {
        "query": "vector database",
        "sort": "stars",
        "max_results": 5
      },
      "reasoningTrace": "Searching GitHub for alternative vector databases"
    }
  ],
  "retrievedDocuments": [
    "Document 1 content...",
    "Document 2 content..."
  ],
  "metrics": {
    "toolCallsCount": 2,
    "documentsRetrieved": 3,
    "durationMs": 2450,
    "estimatedCost": 0.0045,
    "toolUsageCounts": {
      "rag_search": 1,
      "github_search_repositories": 1
    }
  }
}
```

### Get Available Tools

**GET /agent/tools**

Lists all registered tools.

**Response**:
```json
[
  {
    "name": "rag_search",
    "description": "Search through ingested documents using semantic similarity",
    "category": "RAG",
    "parameters": [
      {
        "name": "query",
        "description": "The search query or question",
        "type": "string",
        "required": true
      },
      {
        "name": "top_k",
        "description": "Number of results to return",
        "type": "number",
        "required": false,
        "defaultValue": 3
      }
    ]
  },
  {
    "name": "github_search_repositories",
    "description": "Search for GitHub repositories",
    "category": "GitHub",
    "parameters": [...]
  }
]
```

### Get Specific Tool

**GET /agent/tools/{toolName}**

Get details about a specific tool.

### Ingest Codebase

**POST /agent/ingest-codebase**

Ingest a local codebase for AI-powered code understanding.

**Request**:
```json
{
  "directoryPath": "D:\\Projects\\MyProject\\src",
  "includePatterns": ["*.cs", "*.py", "*.js"],
  "excludePatterns": ["*/bin/*", "*/obj/*", "*/node_modules/*"],
  "parseSemanticStructure": true,
  "chunkSize": 1000
}
```

**Response**:
```json
{
  "totalFiles": 45,
  "totalLines": 12340,
  "chunksCreated": 234,
  "filesProcessed": [
    "Program.cs",
    "Startup.cs",
    "...
  ],
  "extractedElements": 128,
  "durationSeconds": 15.3
}
```

### Search Code

**POST /agent/search-code**

Search ingested codebase semantically.

**Request**:
```json
{
  "query": "How does authentication work in this codebase?",
  "topK": 5
}
```

**Response**:
```json
[
  {
    "filePath": "Controllers/AuthController.cs",
    "codeSnippet": "public async Task<IActionResult> Login(...)",
    "relevanceScore": 0.89
  },
  {
    "filePath": "Services/JwtService.cs",
    "codeSnippet": "public string GenerateToken(...)",
    "relevanceScore": 0.85
  }
]
```

### Get Code Context

**GET /agent/code-context?filePath={path}&startLine={start}&endLine={end}**

Get detailed context for a specific code file.

## 🧪 Testing Guide

### Step 1: Basic Agent Query

```bash
# Start the API
dotnet run --project src/Rag.Api

# Chat with agent (uses RAG)
curl -X POST http://localhost:5129/agent/chat \
  -H "Content-Type: application/json" \
  -H "X-API-Key: secure_password" \
  -H "X-Tenant-Id: test-tenant" \
  -d '{
    "message": "What is Qdrant? Search the documentation.",
    "config": {
      "maxToolCalls": 3,
      "useRagForContext": true
    }
  }'
```

**Expected Behavior**:
1. Agent recognizes need to search documents
2. Calls `rag_search` tool
3. Retrieves relevant documents
4. Synthesizes answer

### Step 2: GitHub Research

```bash
curl -X POST http://localhost:5129/agent/chat \
  -H "Content-Type: application/json" \
  -H "X-API-Key: secure_password" \
  -d '{
    "message": "Find the top 5 most starred vector database projects on GitHub",
    "config": {
      "maxToolCalls": 2
    }
  }'
```

**Expected Behavior**:
1. Agent calls `github_search_repositories` tool
2. Returns list of repositories
3. Formats results nicely

### Step 3: Multi-Tool Research

```bash
curl -X POST http://localhost:5129/agent/chat \
  -H "Content-Type: application/json" \
  -H "X-API-Key: secure_password" \
  -H "X-Tenant-Id: test-tenant" \
  -d '{
    "message": "Research vector databases. First check our docs, then search GitHub for alternatives, and finally find code examples.",
    "config": {
      "maxToolCalls": 5,
      "allowParallelToolCalls": false,
      "enableChainOfThought": true
    }
  }'
```

**Expected Behavior**:
1. Agent plans multi-step execution
2. Calls `rag_search` first
3. Then calls `github_search_repositories`
4. Then calls `github_search_code`
5. Synthesizes comprehensive answer

### Step 4: Codebase Ingestion

```bash
# Ingest your codebase
curl -X POST http://localhost:5129/agent/ingest-codebase \
  -H "Content-Type: application/json" \
  -H "X-API-Key: secure_password" \
  -H "X-Tenant-Id: my-project" \
  -d '{
    "directoryPath": "D:\\Projects\\MyProject\\src",
    "includePatterns": ["*.cs"],
    "excludePatterns": ["*/bin/*", "*/obj/*"],
    "parseSemanticStructure": true
  }'

# Search the ingested code
curl -X POST http://localhost:5129/agent/search-code \
  -H "Content-Type: application/json" \
  -H "X-API-Key: secure_password" \
  -H "X-Tenant-Id: my-project" \
  -d '{
    "query": "Show me how authentication is implemented",
    "topK": 5
  }'

# Ask agent about the code
curl -X POST http://localhost:5129/agent/chat \
  -H "Content-Type: application/json" \
  -H "X-API-Key: secure_password" \
  -H "X-Tenant-Id: my-project" \
  -d '{
    "message": "Explain the authentication flow in this codebase",
    "config": {
      "useRagForContext": true
    }
  }'
```

## 💡 Advanced Use Cases

### 1. Research Assistant

```bash
POST /agent/chat
{
  "message": "I need to implement a vector database for my project. Research the options, compare their features, and recommend the best one based on our requirements.",
  "config": {
    "maxToolCalls": 10,
    "allowParallelToolCalls": true,
    "enableChainOfThought": true,
    "systemPrompt": "You are a technical research assistant. Provide detailed, well-researched answers with sources."
  }
}
```

**Agent will**:
- Search internal docs for existing knowledge
- Search GitHub for popular vector databases
- Search GitHub code for implementation examples
- Compare features
- Provide recommendation

### 2. Code Review Assistant

```bash
# 1. Ingest codebase
POST /agent/ingest-codebase
{
  "directoryPath": "D:\\Projects\\MyProject\\src",
  "includePatterns": ["*.cs"],
  "parseSemanticStructure": true
}

# 2. Ask agent to review
POST /agent/chat
{
  "message": "Review the authentication implementation in this codebase. Check for security vulnerabilities, code quality issues, and suggest improvements.",
  "config": {
    "useRagForContext": true,
    "enableChainOfThought": true
  }
}
```

### 3. Documentation Generator

```bash
POST /agent/chat
{
  "message": "Generate comprehensive documentation for the AgentOrchestrator class. Include purpose, architecture, usage examples, and API reference.",
  "config": {
    "useRagForContext": true,
    "systemPrompt": "You are a technical writer. Generate clear, comprehensive documentation."
  }
}
```

### 4. Learning Assistant

```bash
POST /agent/chat
{
  "message": "I want to learn about RAG systems. Find tutorials, code examples, and best practices. Start with our docs, then find GitHub examples.",
  "config": {
    "maxToolCalls": 5,
    "enableChainOfThought": true
  }
}
```

## 🔧 Creating Custom Tools

### Step 1: Implement ITool

```csharp
public class WeatherTool : ITool
{
    public string Name => "get_weather";
    
    public string Description => "Get current weather for a city";
    
    public IReadOnlyList<ToolParameter> Parameters => new List<ToolParameter>
    {
        new("city", "City name", "string", true),
        new("units", "Temperature units (celsius/fahrenheit)", "string", false, "celsius")
    };
    
    public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> arguments, CancellationToken ct)
    {
        var city = arguments["city"].ToString()!;
        var units = arguments.TryGetValue("units", out var u) ? u.ToString() : "celsius";
        
        // Call weather API
        var temperature = await GetWeatherAsync(city, units, ct);
        
        return ToolResult.Ok(
            $"The temperature in {city} is {temperature}°{units}",
            new Dictionary<string, object>
            {
                ["city"] = city,
                ["temperature"] = temperature,
                ["units"] = units
            }
        );
    }
}
```

### Step 2: Register Tool

```csharp
// In Program.cs
var weatherTool = new WeatherTool();
registry.RegisterTool(weatherTool, new ToolMetadata(
    weatherTool.Name,
    weatherTool.Description,
    ToolCategory.Custom,
    new List<string> { "weather", "forecast", "temperature" }
));
```

### Step 3: Use Tool

```bash
POST /agent/chat
{
  "message": "What's the weather like in Seattle?",
  "config": {
    "maxToolCalls": 2
  }
}
```

Agent will automatically discover and use the `get_weather` tool!

## 📈 Performance Considerations

### Parallel vs Sequential Tool Execution

**Parallel** (faster but less context):
```json
{
  "allowParallelToolCalls": true
}
```
- All tools execute simultaneously
- Faster for independent operations
- No inter-tool dependencies

**Sequential** (slower but smarter):
```json
{
  "allowParallelToolCalls": false
}
```
- Tools execute one at a time
- Later tools can use earlier results
- Enables complex multi-step reasoning

### Cost Optimization

```json
{
  "maxToolCalls": 3,                    // Limit tool calls
  "useRagForContext": true,             // Pre-fetch relevant docs
  "topKDocuments": 3,                   // Limit retrieved docs
  "enableChainOfThought": false         // Disable if not needed
}
```

**Estimated Costs**:
- Basic agent query: $0.002 - $0.005
- Multi-tool research: $0.01 - $0.03
- Codebase ingestion (1000 files): $0.50 - $1.00

## 🎓 Architecture Patterns

### Pattern 1: RAG-First Agent

```json
{
  "config": {
    "useRagForContext": true,
    "systemPrompt": "Always search documents before answering questions."
  }
}
```

Agent always searches internal knowledge first.

### Pattern 2: Hybrid Agent

```json
{
  "config": {
    "maxToolCalls": 5,
    "allowParallelToolCalls": true
  }
}
```

Agent decides when to use RAG vs external tools.

### Pattern 3: Research Agent

```json
{
  "config": {
    "maxToolCalls": 10,
    "enableChainOfThought": true,
    "systemPrompt": "You are a research assistant. Always cite sources and provide comprehensive answers."
  }
}
```

Agent does deep research across multiple tools.

## ✅ Summary

Phase 5 Agent Layer provides:

- ✅ **MCP-Like Tool Abstraction**: Standardized, composable tools
- ✅ **Agent Orchestration**: Intelligent planning and execution
- ✅ **3 Built-In Tools**: RAG search, GitHub repos, GitHub code
- ✅ **Codebase Ingestion**: AI-powered code understanding
- ✅ **Multi-Step Reasoning**: Complex task decomposition
- ✅ **Parallel Execution**: Faster tool execution
- ✅ **Extensible**: Easy to add custom tools

**What Makes This Special**:
- Most RAG systems are static Q&A
- Agents can **plan, research, and execute**
- Tool-calling enables unlimited capabilities
- MCP-inspired architecture is cutting-edge

**Next Steps**:
1. Test agent with sample queries
2. Create custom tools for your domain
3. Ingest your codebase
4. Build specialized agents (research, code review, etc.)
5. Deploy as intelligent API

**File Structure**:
```
src/Rag.Core/Agent/
├── ToolModels.cs                      # Tool abstractions (ITool, ToolResult, etc.)
├── IToolRegistry.cs                   # Tool registry interface
├── IAgentOrchestrator.cs              # Agent orchestration interface
└── ICodebaseIngestionService.cs       # Codebase ingestion interface

src/Rag.Infrastructure/Agent/
├── ToolRegistry.cs                    # Tool registry implementation
├── ToolExecutor.cs                    # Tool execution engine
├── AgentOrchestrator.cs               # Main agent brain (320+ LOC)
├── CodebaseIngestionService.cs        # Codebase parser & indexer
└── Tools/
    ├── RagSearchTool.cs               # RAG search tool
    └── GitHubSearchTool.cs            # GitHub integration tools

src/Rag.Api/Controllers/
└── AgentController.cs                 # Agent REST API (6 endpoints)

agent-examples/
└── sample-agent-queries.json          # Example queries & use cases
```

---

**Phase 5 transforms your RAG system into an autonomous AI agent capable of research, planning, and execution. This is the future of AI applications!** 🚀
