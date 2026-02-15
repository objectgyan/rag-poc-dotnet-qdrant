# Phase 10: Agent Tool Expansion 🤖

**Priority**: MEDIUM | **Complexity**: Medium | **Status**: NOT STARTED

## Overview
Expand agent capabilities with additional tools: web scraping, SQL queries, calculator, and long-term memory for persistent conversations.

---

## Goals
- ✅ Web scraping tool (extract content from URLs)
- ✅ SQL query tool (safe read-only database access)
- ✅ Calculator/Math evaluation tool
- ✅ Long-term memory (conversation persistence)
- ✅ File system tool (safe file operations)

---

## Current Tools (Already Implemented)
- ✅ RAG Tool (document search and retrieval)
- ✅ Code Search Tool (semantic code search)
- ✅ Code Context Tool (get file context)

---

## New Tools to Implement

### 1. Web Scraping Tool 🌐
**Purpose**: Allow agent to fetch and extract content from web pages

**Files to create**:
- `src/Rag.Core/Agent/Tools/WebScraperTool.cs`
- `src/Rag.Infrastructure/WebScraping/WebScraperService.cs`
- `src/Rag.Core/Abstractions/IWebScraperService.cs`

**Features**:
- Fetch URL content
- Extract main text content (remove ads, navigation, etc.)
- Handle JavaScript-rendered pages
- Respect robots.txt
- Rate limiting per domain

**Tool Definition**:
```csharp
public class WebScraperTool : ITool
{
    public string Name => "web_scraper";
    public string Description => "Fetch and extract content from web pages. Returns cleaned text content.";
    
    public List<ToolParameter> Parameters => new()
    {
        new("url", "The URL to scrape", "string", required: true),
        new("selector", "CSS selector to extract specific content (optional)", "string", required: false),
        new("includeLinks", "Whether to include links in the result", "boolean", required: false)
    };
}
```

**Safety Measures**:
- URL whitelist/blacklist
- Content size limits
- Timeout for slow pages
- Anti-bot detection handling

**NuGet Packages**:
```xml
<PackageReference Include="AngleSharp" Version="1.1.0" />
<PackageReference Include="HtmlAgilityPack" Version="1.11.59" />
<!-- Optional for JS rendering -->
<PackageReference Include="PuppeteerSharp" Version="18.0.0" />
```

---

### 2. SQL Query Tool 🗄️
**Purpose**: Execute safe, read-only SQL queries against configured databases

**Files to create**:
- `src/Rag.Core/Agent/Tools/SqlQueryTool.cs`
- `src/Rag.Infrastructure/Database/SqlQueryService.cs`
- `src/Rag.Core/Abstractions/ISqlQueryService.cs`

**Features**:
- Read-only query execution
- Query validation and sanitization
- Result formatting (JSON, CSV, table)
- Connection pooling
- Query timeout enforcement

**Tool Definition**:
```csharp
public class SqlQueryTool : ITool
{
    public string Name => "sql_query";
    public string Description => "Execute read-only SQL queries. Returns query results as JSON.";
    
    public List<ToolParameter> Parameters => new()
    {
        new("query", "The SQL query to execute (SELECT only)", "string", required: true),
        new("database", "Database name to query", "string", required: false),
        new("maxRows", "Maximum rows to return (default: 100)", "integer", required: false)
    };
}
```

**Safety Measures**:
- Only allow SELECT statements
- Block dangerous keywords (DROP, DELETE, UPDATE, etc.)
- Row limits (max 1000 rows)
- Query timeout (5 seconds)
- Rate limiting per tenant
- Parameterized queries only

**Configuration**:
```json
{
  "SqlQueryTool": {
    "Enabled": true,
    "AllowedDatabases": ["analytics", "reporting"],
    "MaxRows": 1000,
    "QueryTimeout": 5000,
    "BlockedKeywords": ["DROP", "DELETE", "UPDATE", "INSERT", "EXEC"]
  }
}
```

---

### 3. Calculator/Math Tool 🧮
**Purpose**: Evaluate mathematical expressions and perform calculations

**Files to create**:
- `src/Rag.Core/Agent/Tools/CalculatorTool.cs`
- `src/Rag.Infrastructure/Math/MathEvaluatorService.cs`

**Features**:
- Basic arithmetic (+, -, *, /, %)
- Advanced functions (sqrt, pow, log, sin, cos, etc.)
- Unit conversions
- Date/time calculations
- Financial calculations (compound interest, NPV, etc.)

**Tool Definition**:
```csharp
public class CalculatorTool : ITool
{
    public string Name => "calculator";
    public string Description => "Evaluate mathematical expressions. Supports arithmetic, algebra, trigonometry, and unit conversions.";
    
    public List<ToolParameter> Parameters => new()
    {
        new("expression", "The mathematical expression to evaluate", "string", required: true),
        new("precision", "Number of decimal places (default: 2)", "integer", required: false)
    };
}
```

**Examples**:
```
"2 + 2" → 4
"sqrt(16) + pow(2, 3)" → 12
"100 USD to EUR" → 92.5 EUR
"days between 2024-01-01 and 2024-12-31" → 366 days
```

**NuGet Packages**:
```xml
<PackageReference Include="NCalc" Version="3.0.0" />
<PackageReference Include="UnitsNet" Version="5.50.0" />
```

---

### 4. Long-term Memory Tool 🧠
**Purpose**: Store and retrieve conversation context across sessions

**Files to create**:
- `src/Rag.Core/Agent/Tools/MemoryTool.cs`
- `src/Rag.Infrastructure/Memory/ConversationMemoryService.cs`
- `src/Rag.Core/Abstractions/IConversationMemory.cs`
- `src/Rag.Core/Models/ConversationMemory.cs`

**Features**:
- Store key facts from conversations
- Retrieve relevant past context
- Semantic search over conversation history
- Auto-summarization of long conversations
- Memory pruning (keep important, discard trivial)

**Tool Definition**:
```csharp
public class MemoryTool : ITool
{
    public string Name => "memory";
    public string Description => "Store and retrieve information from conversation history.";
    
    public List<ToolParameter> Parameters => new()
    {
        new("action", "Action to perform: 'store', 'retrieve', 'search'", "string", required: true),
        new("content", "Content to store or search query", "string", required: true),
        new("userId", "User identifier for memory isolation", "string", required: false),
        new("category", "Memory category (e.g., 'preference', 'fact', 'task')", "string", required: false)
    };
}
```

**Storage Strategy**:
- Store in Qdrant (reuse vector infrastructure)
- Separate collection: `conversation_memory`
- Metadata: userId, timestamp, category, importance
- TTL-based expiration (configurable)

**Memory Types**:
```csharp
public enum MemoryType
{
    Fact,          // "User prefers dark mode"
    Preference,    // "Likes detailed explanations"
    Task,          // "Working on RAG system"
    Context,       // "Current project is e-commerce"
    Goal           // "Learn about vector databases"
}
```

---

### 5. File System Tool 📁
**Purpose**: Safe file operations for reading/writing files

**Files to create**:
- `src/Rag.Core/Agent/Tools/FileSystemTool.cs`
- `src/Rag.Infrastructure/FileSystem/SafeFileSystemService.cs`

**Features**:
- Read file contents
- List directory contents
- Check file existence
- Get file metadata
- Safe write operations (sandboxed)

**Tool Definition**:
```csharp
public class FileSystemTool : ITool
{
    public string Name => "file_system";
    public string Description => "Perform safe file system operations (read, list, metadata).";
    
    public List<ToolParameter> Parameters => new()
    {
        new("action", "Action: 'read', 'list', 'exists', 'metadata'", "string", required: true),
        new("path", "File or directory path", "string", required: true),
        new("pattern", "File pattern for list operation (e.g., '*.txt')", "string", required: false)
    };
}
```

**Safety Measures**:
- Sandboxed access (whitelist directories only)
- No access to system directories
- File size limits (max 10MB)
- Block binary files (only text files)
- Path traversal prevention

---

## Implementation Tasks

### Task 1: Web Scraper Tool
**Subtasks**:
1. Create `IWebScraperService` interface
2. Implement `WebScraperService` with AngleSharp
3. Add URL validation and sanitization
4. Implement rate limiting per domain
5. Create `WebScraperTool` and register
6. Add configuration options
7. Write unit tests

**Estimated Time**: 4-6 hours

---

### Task 2: SQL Query Tool
**Subtasks**:
1. Create `ISqlQueryService` interface
2. Implement query validation (whitelist SELECT)
3. Add connection management
4. Implement result formatting
5. Create `SqlQueryTool` and register
6. Add security checks
7. Write integration tests

**Estimated Time**: 6-8 hours

---

### Task 3: Calculator Tool
**Subtasks**:
1. Install NCalc package
2. Create `MathEvaluatorService`
3. Implement expression parsing
4. Add unit conversion support
5. Create `CalculatorTool` and register
6. Write unit tests

**Estimated Time**: 3-4 hours

---

### Task 4: Long-term Memory
**Subtasks**:
1. Create `IConversationMemory` interface
2. Implement `ConversationMemoryService`
3. Create Qdrant collection for memory
4. Implement store/retrieve/search operations
5. Add memory pruning logic
6. Create `MemoryTool` and register
7. Write integration tests

**Estimated Time**: 8-10 hours

---

### Task 5: File System Tool
**Subtasks**:
1. Create `ISafeFileSystemService` interface
2. Implement sandboxed file operations
3. Add path validation
4. Create `FileSystemTool` and register
5. Add configuration for allowed directories
6. Write security tests

**Estimated Time**: 4-5 hours

---

## API Integration

### New Endpoints
```http
# Test individual tools
POST /api/v1/agent/tools/execute
{
  "toolName": "web_scraper",
  "arguments": {
    "url": "https://example.com",
    "selector": "article"
  }
}

# List all available tools (already exists)
GET /api/v1/agent/tools

# Get conversation memory
GET /api/v1/agent/memory?userId={userId}

# Clear memory
DELETE /api/v1/agent/memory?userId={userId}
```

---

## Configuration

```json
{
  "AgentTools": {
    "WebScraper": {
      "Enabled": true,
      "AllowedDomains": ["*"],
      "BlockedDomains": ["localhost", "127.0.0.1"],
      "MaxContentSize": 5242880,
      "Timeout": 10000,
      "UserAgent": "RagPoc-Agent/1.0"
    },
    "SqlQuery": {
      "Enabled": false,
      "ConnectionStrings": {
        "analytics": "Server=...;Database=analytics;..."
      },
      "MaxRows": 1000,
      "QueryTimeout": 5000
    },
    "Calculator": {
      "Enabled": true,
      "MaxPrecision": 10
    },
    "Memory": {
      "Enabled": true,
      "MaxMemoriesPerUser": 1000,
      "DefaultTTL": "30.00:00:00",
      "AutoPrune": true
    },
    "FileSystem": {
      "Enabled": false,
      "AllowedDirectories": [
        "C:\\workspace\\uploads",
        "C:\\workspace\\temp"
      ],
      "MaxFileSize": 10485760
    }
  }
}
```

---

## Testing Strategy

### Unit Tests
- Tool parameter validation
- Expression evaluation correctness
- Query sanitization
- Path validation

### Integration Tests
- End-to-end tool execution
- Agent tool selection logic
- Memory persistence
- Rate limiting

### Security Tests
- SQL injection prevention
- Path traversal prevention
- XSS in scraped content
- Resource exhaustion

---

## Success Criteria
- ✅ All 5 tools working and registered
- ✅ Agent can select and use tools correctly
- ✅ No security vulnerabilities
- ✅ < 100ms tool execution time (except web scraping)
- ✅ Comprehensive test coverage > 80%

---

## Example Agent Conversations

**With Web Scraper**:
```
User: What's the latest news on OpenAI's website?
Agent: [Uses web_scraper tool on openai.com/blog]
      Latest post: "Introducing GPT-5..."
```

**With SQL Query**:
```
User: Show me top 5 customers by revenue
Agent: [Uses sql_query: SELECT TOP 5 customer, SUM(revenue) ...]
      1. Acme Corp - $1.2M
      2. TechStart - $850K
      ...
```

**With Calculator**:
```
User: If I invest $10,000 at 7% annual interest for 5 years, what's the final amount?
Agent: [Uses calculator: 10000 * pow(1.07, 5)]
      Your investment will grow to $14,025.52
```

**With Memory**:
```
User: Remember that I prefer Python over JavaScript
Agent: [Uses memory tool to store preference]
      Got it! I'll remember you prefer Python.

[Later session]
User: Can you help me with a web scraper?
Agent: [Retrieves memory, sees Python preference]
      Sure! Here's a Python web scraper using BeautifulSoup...
```

---

## Next Phase
After completion → **Phase 11: Production Infrastructure**
