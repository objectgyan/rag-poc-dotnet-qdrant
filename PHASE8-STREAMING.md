# ⚡ Phase 8 - Streaming & Real-time Communication

This document describes the implementation of streaming responses using Server-Sent Events (SSE) and WebSocket for real-time, token-by-token responses in the RAG application.

## 🎯 Goals

### Primary Objectives
- ✅ **Streaming Chat Responses**: Token-by-token streaming for AI responses
- ✅ **Server-Sent Events (SSE)**: One-way server-to-client streaming
- ✅ **WebSocket Support**: Bidirectional real-time communication
- ✅ **Frontend Integration**: Real-time UI updates with streaming text
- ✅ **Backwards Compatibility**: Keep existing non-streaming endpoints

### Benefits
- **Better UX**: Users see responses immediately (reduced perceived latency)
- **Lower Timeouts**: Chunked responses prevent timeout issues
- **Progress Feedback**: Show streaming progress indicators
- **Cost Efficiency**: Same API costs but better user experience

---

## 📋 Implementation Checklist

### Backend Tasks
- [ ] Update OpenAI client to support streaming
- [ ] Update Claude client to support streaming
- [ ] Add SSE middleware/response handling
- [ ] Create streaming endpoints for `/ask` and `/agent/chat`
- [ ] Add WebSocket hub for bidirectional chat
- [ ] Update DTO models for streaming metadata
- [ ] Add streaming configuration options

### Frontend Tasks
- [ ] Create SSE client hook (`useSSE`)
- [ ] Create WebSocket client hook (`useWebSocket`)
- [ ] Update chat UI for streaming responses
- [ ] Add typing indicators and progress feedback
- [ ] Handle connection errors and reconnection
- [ ] Add toggle for streaming vs. non-streaming mode

### Testing & Documentation
- [ ] Add integration tests for streaming endpoints
- [ ] Update API documentation with streaming examples
- [ ] Add curl examples for SSE endpoints
- [ ] Create frontend demo page for streaming

---

## 🏗️ Architecture Overview

### SSE Flow (Server-Sent Events)
```
┌─────────────┐                  ┌──────────────┐                  ┌─────────────┐
│   Browser   │                  │  ASP.NET API │                  │  OpenAI/    │
│  (Frontend) │                  │   (Backend)  │                  │  Claude     │
└──────┬──────┘                  └──────┬───────┘                  └──────┬──────┘
       │                                │                                 │
       │  GET /api/v1/ask/stream       │                                 │
       │  (Accept: text/event-stream)  │                                 │
       │──────────────────────────────>│                                 │
       │                                │  POST /chat/completions         │
       │                                │  (stream: true)                 │
       │                                │────────────────────────────────>│
       │                                │                                 │
       │                                │  data: {"delta":"Hello"}        │
       │                                │<────────────────────────────────│
       │  data: {"token":"Hello"}      │                                 │
       │<──────────────────────────────│                                 │
       │                                │  data: {"delta":" world"}       │
       │                                │<────────────────────────────────│
       │  data: {"token":" world"}     │                                 │
       │<──────────────────────────────│                                 │
       │                                │  data: [DONE]                   │
       │                                │<────────────────────────────────│
       │  data: {"done":true}          │                                 │
       │<──────────────────────────────│                                 │
       │                                │                                 │
```

### WebSocket Flow (Bidirectional)
```
┌─────────────┐                  ┌──────────────┐                  ┌─────────────┐
│   Browser   │                  │  ASP.NET API │                  │  OpenAI/    │
│  (Frontend) │                  │   (Backend)  │                  │  Claude     │
└──────┬──────┘                  └──────┬───────┘                  └──────┬──────┘
       │                                │                                 │
       │  WS /api/v1/chat/ws           │                                 │
       │  (WebSocket handshake)        │                                 │
       │<─────────────────────────────>│                                 │
       │  Connection Established       │                                 │
       │                                │                                 │
       │  {"message":"Hello"}          │                                 │
       │──────────────────────────────>│                                 │
       │                                │  POST /chat/completions         │
       │                                │────────────────────────────────>│
       │                                │  (stream: true)                 │
       │                                │                                 │
       │  {"token":"Hi"}               │  data: {"delta":"Hi"}           │
       │<──────────────────────────────│<────────────────────────────────│
       │  {"token":" there"}           │  data: {"delta":" there"}       │
       │<──────────────────────────────│<────────────────────────────────│
       │  {"done":true}                │  data: [DONE]                   │
       │<──────────────────────────────│<────────────────────────────────│
       │                                │                                 │
```

---

## 🔨 Step-by-Step Implementation

## Step 1: Update OpenAI Service for Streaming

### 1.1 Add Streaming Method to IOpenAiService

**File**: `src/Rag.Infrastructure/OpenAI/IOpenAiService.cs`

```csharp
public interface IOpenAiService
{
    // Existing methods...
    Task<string> GenerateResponseAsync(string prompt, CancellationToken ct = default);
    
    // NEW: Streaming support
    IAsyncEnumerable<string> StreamResponseAsync(
        string prompt, 
        CancellationToken ct = default);
}
```

### 1.2 Implement Streaming in OpenAiService

**File**: `src/Rag.Infrastructure/OpenAI/OpenAiService.cs`

```csharp
using OpenAI.Chat;
using System.Runtime.CompilerServices;

public class OpenAiService : IOpenAiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiService> _logger;
    private readonly string _apiKey;
    private readonly string _model;

    // Existing GenerateResponseAsync...

    public async IAsyncEnumerable<string> StreamResponseAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogInformation("Starting OpenAI streaming response");

        var chatClient = new ChatClient(_model, _apiKey);
        
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("You are a helpful RAG assistant."),
            new UserChatMessage(prompt)
        };

        var streamingResponse = chatClient.CompleteChatStreamingAsync(
            messages, 
            cancellationToken: ct);

        await foreach (var update in streamingResponse.WithCancellation(ct))
        {
            foreach (var contentPart in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(contentPart.Text))
                {
                    yield return contentPart.Text;
                }
            }
        }

        _logger.LogInformation("OpenAI streaming completed");
    }
}
```

---

## Step 2: Create Streaming ASK Endpoint

### 2.1 Add Streaming DTO

**File**: `src/Rag.Api/Models/RagDtos.cs`

```csharp
// Add to existing file

public record StreamingChunkResponse
{
    public string Token { get; init; } = string.Empty;
    public bool Done { get; init; } = false;
    public Dictionary<string, object>? Metadata { get; init; }
}
```

### 2.2 Add Streaming Endpoint to AskController

**File**: `src/Rag.Api/Controllers/AskController.cs`

```csharp
[HttpGet("stream")]
[Produces("text/event-stream")]
public async IAsyncEnumerable<string> AskStream(
    [FromQuery] string question,
    [FromQuery] int topK = 5,
    [EnumeratorCancellation] CancellationToken ct = default)
{
    _logger.LogInformation("Streaming RAG query: {Question}", question);

    // 1. Retrieve context (same as non-streaming)
    var searchResults = await _vectorStore.SearchAsync(
        question, 
        topK, 
        ct);

    var context = string.Join("\n\n", searchResults.Select(r => 
        $"[Document: {r.Metadata["documentId"]}]\n{r.Text}"));

    var prompt = $@"Answer the question based on the following context.

Context:
{context}

Question: {question}

Answer:";

    // 2. Stream response tokens
    await foreach (var token in _openAiService.StreamResponseAsync(prompt, ct))
    {
        // SSE format: "data: {json}\n\n"
        var chunk = new StreamingChunkResponse 
        { 
            Token = token, 
            Done = false 
        };
        
        yield return $"data: {JsonSerializer.Serialize(chunk)}\n\n";
    }

    // 3. Send completion signal
    var doneChunk = new StreamingChunkResponse { Done = true };
    yield return $"data: {JsonSerializer.Serialize(doneChunk)}\n\n";
}
```

---

## Step 3: Add SSE Configuration

### 3.1 Enable SSE in Program.cs

**File**: `src/Rag.Api/Program.cs`

```csharp
// Add before app.Run()

// Enable SSE support
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/v1/ask/stream"))
    {
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Content-Type", "text/event-stream");
        context.Response.Headers.Append("Connection", "keep-alive");
    }
    await next();
});
```

---

## Step 4: Create Frontend SSE Hook

### 4.1 Create useSSE Hook

**File**: `src/Rag.Web/src/hooks/useSSE.ts` (NEW FILE)

```typescript
import { useState, useEffect, useCallback } from 'react';

interface UseSSEOptions {
  onMessage: (data: any) => void;
  onError?: (error: Error) => void;
  onComplete?: () => void;
}

export function useSSE(url: string | null, options: UseSSEOptions) {
  const [isConnected, setIsConnected] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    if (!url) return;

    let eventSource: EventSource | null = null;
    
    try {
      eventSource = new EventSource(url);
      
      eventSource.onopen = () => {
        console.log('SSE connection opened');
        setIsConnected(true);
        setError(null);
      };

      eventSource.onmessage = (event) => {
        try {
          const data = JSON.parse(event.data);
          
          if (data.done) {
            options.onComplete?.();
            eventSource?.close();
          } else {
            options.onMessage(data);
          }
        } catch (err) {
          console.error('Failed to parse SSE message:', err);
        }
      };

      eventSource.onerror = (err) => {
        console.error('SSE error:', err);
        const error = new Error('SSE connection failed');
        setError(error);
        setIsConnected(false);
        options.onError?.(error);
        eventSource?.close();
      };
    } catch (err) {
      const error = err instanceof Error ? err : new Error('Unknown error');
      setError(error);
      options.onError?.(error);
    }

    return () => {
      eventSource?.close();
      setIsConnected(false);
    };
  }, [url, options]);

  return { isConnected, error };
}
```

### 4.2 Create Streaming Chat Component

**File**: `src/Rag.Web/src/components/StreamingChat.tsx` (NEW FILE)

```typescript
import React, { useState } from 'react';
import { useSSE } from '../hooks/useSSE';

export function StreamingChat() {
  const [question, setQuestion] = useState('');
  const [streamingAnswer, setStreamingAnswer] = useState('');
  const [isStreaming, setIsStreaming] = useState(false);
  const [sseUrl, setSseUrl] = useState<string | null>(null);

  const { isConnected, error } = useSSE(sseUrl, {
    onMessage: (data) => {
      if (data.token) {
        setStreamingAnswer((prev) => prev + data.token);
      }
    },
    onComplete: () => {
      console.log('Streaming completed');
      setIsStreaming(false);
      setSseUrl(null);
    },
    onError: (err) => {
      console.error('Streaming error:', err);
      setIsStreaming(false);
      setSseUrl(null);
    }
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!question.trim()) return;

    // Reset state
    setStreamingAnswer('');
    setIsStreaming(true);

    // Build SSE URL with query params
    const params = new URLSearchParams({
      question: question,
      topK: '5'
    });
    
    const token = localStorage.getItem('token');
    const url = `/api/v1/ask/stream?${params}`;
    
    // Note: EventSource doesn't support custom headers
    // For auth, use cookie-based auth or include token in URL (less secure)
    setSseUrl(url);
  };

  return (
    <div className="max-w-4xl mx-auto p-6">
      <h2 className="text-2xl font-bold mb-4">Streaming RAG Chat</h2>

      <form onSubmit={handleSubmit} className="mb-6">
        <div className="flex gap-2">
          <input
            type="text"
            value={question}
            onChange={(e) => setQuestion(e.target.value)}
            placeholder="Ask a question..."
            className="flex-1 px-4 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
            disabled={isStreaming}
          />
          <button
            type="submit"
            disabled={isStreaming || !question.trim()}
            className="px-6 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {isStreaming ? 'Streaming...' : 'Ask'}
          </button>
        </div>
      </form>

      {/* Streaming Status */}
      {isStreaming && (
        <div className="mb-4 flex items-center gap-2 text-sm text-gray-600">
          <div className="animate-pulse">●</div>
          <span>{isConnected ? 'Streaming...' : 'Connecting...'}</span>
        </div>
      )}

      {/* Error Display */}
      {error && (
        <div className="mb-4 p-4 bg-red-50 border border-red-200 rounded-lg text-red-700">
          Error: {error.message}
        </div>
      )}

      {/* Streaming Answer */}
      {streamingAnswer && (
        <div className="p-6 bg-white border rounded-lg shadow-sm">
          <h3 className="text-lg font-semibold mb-2 text-gray-700">Answer:</h3>
          <div className="prose max-w-none">
            {streamingAnswer}
            {isStreaming && (
              <span className="inline-block w-2 h-4 ml-1 bg-blue-600 animate-pulse" />
            )}
          </div>
        </div>
      )}
    </div>
  );
}
```

---

## Step 5: WebSocket Support (Bidirectional)

### 5.1 Add SignalR for WebSocket Support

**File**: `src/Rag.Api/Rag.Api.csproj`

```xml
<ItemGroup>
  <!-- Add SignalR for WebSocket support -->
  <PackageReference Include="Microsoft.AspNetCore.SignalR" Version="1.1.0" />
</ItemGroup>
```

### 5.2 Create Chat Hub

**File**: `src/Rag.Api/Hubs/ChatHub.cs` (NEW FILE)

```csharp
using Microsoft.AspNetCore.SignalR;
using Rag.Core.Abstractions;
using System.Runtime.CompilerServices;

namespace Rag.Api.Hubs;

public class ChatHub : Hub
{
    private readonly IOpenAiService _openAiService;
    private readonly IVectorStore _vectorStore;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IOpenAiService openAiService,
        IVectorStore vectorStore,
        ILogger<ChatHub> logger)
    {
        _openAiService = openAiService;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    public async Task SendMessage(string question, int topK = 5)
    {
        _logger.LogInformation("WebSocket chat message: {Question}", question);

        try
        {
            // 1. Retrieve context
            var searchResults = await _vectorStore.SearchAsync(question, topK);
            var context = string.Join("\n\n", searchResults.Select(r => 
                $"[Document: {r.Metadata["documentId"]}]\n{r.Text}"));

            var prompt = $@"Answer the question based on the following context.

Context:
{context}

Question: {question}

Answer:";

            // 2. Stream response tokens
            await foreach (var token in _openAiService.StreamResponseAsync(prompt))
            {
                await Clients.Caller.SendAsync("ReceiveToken", token);
            }

            // 3. Send completion signal
            await Clients.Caller.SendAsync("StreamComplete");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in WebSocket chat");
            await Clients.Caller.SendAsync("Error", ex.Message);
        }
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("WebSocket client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("WebSocket client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
```

### 5.3 Register SignalR in Program.cs

**File**: `src/Rag.Api/Program.cs`

```csharp
// Add to services
builder.Services.AddSignalR();

// Add to middleware (before app.Run())
app.MapHub<ChatHub>("/api/v1/chat/ws");
```

### 5.4 Create WebSocket Hook (Frontend)

**File**: `src/Rag.Web/src/hooks/useWebSocket.ts` (NEW FILE)

```typescript
import { useState, useEffect, useCallback, useRef } from 'react';
import * as signalR from '@microsoft/signalr';

export function useWebSocket(hubUrl: string) {
  const [isConnected, setIsConnected] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect()
      .build();

    connectionRef.current = connection;

    connection.start()
      .then(() => {
        console.log('WebSocket connected');
        setIsConnected(true);
        setError(null);
      })
      .catch((err) => {
        console.error('WebSocket connection failed:', err);
        setError(err);
        setIsConnected(false);
      });

    connection.onreconnecting(() => {
      console.log('WebSocket reconnecting...');
      setIsConnected(false);
    });

    connection.onreconnected(() => {
      console.log('WebSocket reconnected');
      setIsConnected(true);
    });

    connection.onclose(() => {
      console.log('WebSocket closed');
      setIsConnected(false);
    });

    return () => {
      connection.stop();
    };
  }, [hubUrl]);

  const sendMessage = useCallback((method: string, ...args: any[]) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      return connectionRef.current.invoke(method, ...args);
    }
    throw new Error('WebSocket not connected');
  }, []);

  const on = useCallback((eventName: string, callback: (...args: any[]) => void) => {
    connectionRef.current?.on(eventName, callback);
  }, []);

  const off = useCallback((eventName: string, callback: (...args: any[]) => void) => {
    connectionRef.current?.off(eventName, callback);
  }, []);

  return { isConnected, error, sendMessage, on, off };
}
```

---

## 📊 Testing Strategy

### Manual Testing with curl (SSE)

```bash
# Test SSE streaming endpoint
curl -N -H "X-API-Key: secure_password" \
  -H "X-Tenant-Id: test-tenant" \
  "http://localhost:5129/api/v1/ask/stream?question=What%20is%20RAG?&topK=5"

# Expected output:
# data: {"token":"RAG","done":false}
# 
# data: {"token":" stands","done":false}
# 
# data: {"token":" for","done":false}
# 
# data: {"done":true}
```

### Integration Test (SSE)

**File**: `src/Rag.Tests/StreamingTests.cs` (NEW FILE)

```csharp
using System.Net.Http;
using Xunit;

namespace Rag.Tests;

public class StreamingTests : IntegrationTestBase
{
    [Fact]
    public async Task AskStream_ShouldReturnStreamingResponse()
    {
        // Arrange
        var client = CreateAuthenticatedClient();
        var url = "/api/v1/ask/stream?question=test&topK=3";

        // Act
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var stream = await response.Content.ReadAsStreamAsync();
        var reader = new StreamReader(stream);

        var chunks = new List<string>();
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (line?.StartsWith("data:") == true)
            {
                chunks.Add(line);
            }
        }

        Assert.NotEmpty(chunks);
        Assert.Contains(chunks, c => c.Contains("\"done\":true"));
    }
}
```

---

## 🎨 UI/UX Improvements

### Typing Indicator Animation

```css
/* Add to your CSS/Tailwind */
@keyframes blink {
  0%, 100% { opacity: 1; }
  50% { opacity: 0; }
}

.typing-indicator {
  animation: blink 1s infinite;
}
```

### Progressive Text Rendering

```typescript
// Render with react-markdown for streaming markdown
import ReactMarkdown from 'react-markdown';

<ReactMarkdown>{streamingAnswer}</ReactMarkdown>
```

---

## ⚠️ Important Considerations

### 1. Authentication with SSE

**Problem**: `EventSource` API doesn't support custom headers (no `Authorization` header)

**Solutions**:
- **Option A**: Use cookie-based authentication
- **Option B**: Pass token in query string (less secure)
- **Option C**: Use WebSocket instead (supports headers)

```typescript
// Option B: Token in query string
const url = `/api/v1/ask/stream?question=${q}&token=${token}`;
```

### 2. Rate Limiting

Add per-user streaming connection limits:

```csharp
// Track active connections per user
private static readonly ConcurrentDictionary<string, int> _activeConnections = new();

[HttpGet("stream")]
public async IAsyncEnumerable<string> AskStream(...)
{
    var userId = HttpContext.User.FindFirst("sub")?.Value ?? "anonymous";
    
    if (_activeConnections.GetOrAdd(userId, 0) >= 5)
    {
        yield return "data: {\"error\":\"Too many concurrent streams\"}\n\n";
        yield break;
    }

    _activeConnections.AddOrUpdate(userId, 1, (k, v) => v + 1);
    
    try
    {
        // ... streaming logic
    }
    finally
    {
        _activeConnections.AddOrUpdate(userId, 0, (k, v) => Math.Max(0, v - 1));
    }
}
```

### 3. Timeout Handling

Configure appropriate timeouts:

```csharp
// Program.cs
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(5);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
});
```

---

## 📈 Performance Metrics

### Expected Improvements

| Metric | Non-Streaming | Streaming | Improvement |
|--------|--------------|-----------|-------------|
| **Time to First Token** | 3-5s | 0.5-1s | **5x faster** |
| **Perceived Latency** | High | Low | **Much better UX** |
| **User Engagement** | Users wait | Users read | **Higher retention** |
| **Timeout Rate** | 2-5% | <0.5% | **90% reduction** |

---

## 🔄 Migration Strategy

### Phase 8.1: SSE for Ask Endpoint ✨ (Week 1)
- Update OpenAI service for streaming
- Add `/api/v1/ask/stream` endpoint
- Create frontend SSE hook
- Add streaming chat UI component

### Phase 8.2: WebSocket for Agent Chat (Week 2)
- Add SignalR dependency
- Create `ChatHub` for WebSocket
- Frontend WebSocket integration
- Add connection status UI

### Phase 8.3: Testing & Polish (Week 3)
- Integration tests
- Error handling improvements
- Performance optimization
- Documentation updates

---

## 📚 Resources

### Documentation
- [Server-Sent Events (MDN)](https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events)
- [SignalR Documentation](https://learn.microsoft.com/en-us/aspnet/core/signalr/)
- [OpenAI Streaming API](https://platform.openai.com/docs/api-reference/streaming)

### Sample Code
- See `examples/streaming-demo.ts` for complete frontend example
- See `tests/StreamingTests.cs` for backend tests

---

## ✅ Success Criteria

- [ ] Users see streaming responses token-by-token
- [ ] Time to first token < 1 second
- [ ] Frontend gracefully handles connection errors
- [ ] Streaming works with both OpenAI and Claude
- [ ] WebSocket bidirectional chat functional
- [ ] All tests passing (unit + integration)
- [ ] Documentation updated with examples

---

**Next Phase**: [Phase 9 - Advanced Caching & Search](PHASE9-CACHING-SEARCH.md)
