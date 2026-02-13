# 🚀 API Usage Examples with Phase 1 Security

This document provides practical examples for using the RAG API with the new security features.

## 🔑 Prerequisites

1. **Set API Key** (for production):
   ```json
   // In appsettings.json or appsettings.Production.json
   {
     "Security": {
       "ApiKey": "your-secure-random-api-key"
     }
   }
   ```

2. **For Development** (API key validation disabled):
   - Leave `Security.ApiKey` empty in `appsettings.Development.json`
   - The middleware will skip authentication and log a warning

---

## 📝 Example Requests

### 1. Ingest Documents (with API Key)

```bash
curl -X POST http://localhost:5129/ingest \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your-api-key-here" \
  -d '{
    "documentId": "doc-001",
    "text": "Retrieval-Augmented Generation (RAG) is a technique that combines information retrieval with large language models to provide more accurate and contextual responses."
  }'
```

**Response (Success)**:
```json
{
  "documentId": "doc-001",
  "chunksCreated": 1
}
```

**Response (Rate Limited - 10+ requests/minute)**:
```json
{
  "error": "rate_limit_exceeded",
  "message": "Too many requests. Please try again later.",
  "retryAfter": 45.2
}
```

---

### 2. Ask Questions (with API Key)

```bash
curl -X POST http://localhost:5129/ask \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your-api-key-here" \
  -d '{
    "question": "What is RAG?",
    "topK": 5
  }'
```

**Response (Success)**:
```json
{
  "answer": "RAG (Retrieval-Augmented Generation) is a technique that combines information retrieval with large language models... [doc-001:0]",
  "citations": [
    {
      "documentId": "doc-001",
      "chunkIndex": 0,
      "score": 0.95
    }
  ]
}
```

**Response (Rate Limited - 30+ requests/minute)**:
```json
{
  "error": "rate_limit_exceeded",
  "message": "Too many requests. Please try again later.",
  "retryAfter": 23.8
}
```

---

### 3. Without API Key (Production - Will Fail)

```bash
curl -X POST http://localhost:5129/ask \
  -H "Content-Type: application/json" \
  -d '{
    "question": "What is RAG?",
    "topK": 5
  }'
```

**Response (401 Unauthorized)**:
```json
{
  "error": "unauthorized",
  "message": "API key required. Provide it via 'X-API-Key' header."
}
```

---

### 4. With Invalid API Key

```bash
curl -X POST http://localhost:5129/ask \
  -H "Content-Type: application/json" \
  -H "X-API-Key: wrong-key" \
  -d '{
    "question": "What is RAG?",
    "topK": 5
  }'
```

**Response (401 Unauthorized)**:
```json
{
  "error": "unauthorized",
  "message": "Invalid API key"
}
```

---

## 🔄 Testing Resilience (Retry Logic)

### Simulate Transient Failure

1. **Stop Qdrant temporarily**:
   ```bash
   docker stop qdrant
   ```

2. **Make a request** (will retry 3 times with exponential backoff):
   ```bash
   curl -X POST http://localhost:5129/ask \
     -H "Content-Type: application/json" \
     -H "X-API-Key: your-api-key" \
     -d '{"question":"test","topK":3}'
   ```

3. **Check logs** - you should see:
   ```
   [Warning] Qdrant request failed (Attempt 1/3). Retrying after 500ms. Error: ...
   [Warning] Qdrant request failed (Attempt 2/3). Retrying after 1000ms. Error: ...
   [Warning] Qdrant request failed (Attempt 3/3). Retrying after 2000ms. Error: ...
   [Error] Request failed after 3 retry attempts
   ```

4. **Restart Qdrant**:
   ```bash
   docker start qdrant
   ```

---

## 📊 Rate Limit Testing Script

### Bash Script to Test Rate Limiting

```bash
#!/bin/bash

API_KEY="your-api-key-here"
BASE_URL="http://localhost:5129"

echo "Testing /ask endpoint rate limit (30/min)..."
for i in {1..35}; do
  STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
    -X POST "$BASE_URL/ask" \
    -H "Content-Type: application/json" \
    -H "X-API-Key: $API_KEY" \
    -d '{"question":"test","topK":3}')
  
  if [ "$STATUS" = "200" ]; then
    echo "Request $i: ✅ Success"
  elif [ "$STATUS" = "429" ]; then
    echo "Request $i: ⚠️ Rate Limited (429)"
  else
    echo "Request $i: ❌ Error ($STATUS)"
  fi
  
  sleep 0.1
done
```

### PowerShell Script

```powershell
$apiKey = "your-api-key-here"
$baseUrl = "http://localhost:5129"

Write-Host "Testing /ask endpoint rate limit (30/min)..."
1..35 | ForEach-Object {
    try {
        $response = Invoke-WebRequest -Uri "$baseUrl/ask" `
            -Method Post `
            -Headers @{
                "Content-Type" = "application/json"
                "X-API-Key" = $apiKey
            } `
            -Body '{"question":"test","topK":3}' `
            -ErrorAction Stop
        
        Write-Host "Request $_: ✅ Success ($($response.StatusCode))"
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq 429) {
            Write-Host "Request $_: ⚠️ Rate Limited (429)"
        }
        else {
            Write-Host "Request $_: ❌ Error ($statusCode)"
        }
    }
    Start-Sleep -Milliseconds 100
}
```

---

## 🌐 Using with HTTP Client Libraries

### C# HttpClient

```csharp
using System.Net.Http.Json;

var client = new HttpClient();
client.DefaultRequestHeaders.Add("X-API-Key", "your-api-key-here");

var request = new
{
    question = "What is RAG?",
    topK = 5
};

var response = await client.PostAsJsonAsync("http://localhost:5129/ask", request);

if (response.IsSuccessStatusCode)
{
    var result = await response.Content.ReadFromJsonAsync<AskResponse>();
    Console.WriteLine($"Answer: {result.Answer}");
}
else if (response.StatusCode == HttpStatusCode.TooManyRequests)
{
    Console.WriteLine("Rate limit exceeded. Retry later.");
}
else if (response.StatusCode == HttpStatusCode.Unauthorized)
{
    Console.WriteLine("Invalid or missing API key.");
}
```

### Python (requests)

```python
import requests

headers = {
    "Content-Type": "application/json",
    "X-API-Key": "your-api-key-here"
}

payload = {
    "question": "What is RAG?",
    "topK": 5
}

response = requests.post(
    "http://localhost:5129/ask",
    json=payload,
    headers=headers
)

if response.status_code == 200:
    result = response.json()
    print(f"Answer: {result['answer']}")
elif response.status_code == 429:
    print("Rate limit exceeded. Retry later.")
elif response.status_code == 401:
    print("Invalid or missing API key.")
```

### JavaScript (fetch)

```javascript
const headers = {
  "Content-Type": "application/json",
  "X-API-Key": "your-api-key-here"
};

const payload = {
  question: "What is RAG?",
  topK: 5
};

const response = await fetch("http://localhost:5129/ask", {
  method: "POST",
  headers: headers,
  body: JSON.stringify(payload)
});

if (response.ok) {
  const result = await response.json();
  console.log(`Answer: ${result.answer}`);
} else if (response.status === 429) {
  console.log("Rate limit exceeded. Retry later.");
} else if (response.status === 401) {
  console.log("Invalid or missing API key.");
}
```

---

## 🛠️ Configuration Tips

### Development Environment
```json
{
  "Security": {
    "ApiKey": ""
  },
  "RateLimiting": {
    "AskRequestsPerMinute": 100,
    "IngestRequestsPerMinute": 50,
    "GlobalRequestsPerMinute": 200
  }
}
```

### Production Environment
```json
{
  "Security": {
    "ApiKey": "use-strong-random-key-from-secret-manager"
  },
  "RateLimiting": {
    "AskRequestsPerMinute": 30,
    "IngestRequestsPerMinute": 10,
    "GlobalRequestsPerMinute": 100
  },
  "Resilience": {
    "MaxRetryAttempts": 3,
    "InitialRetryDelayMs": 1000,
    "TimeoutSeconds": 60
  }
}
```

---

## 🎯 Best Practices

1. **Store API keys securely**:
   - Use environment variables
   - Use Azure Key Vault, AWS Secrets Manager, etc.
   - Never commit keys to source control

2. **Handle rate limits gracefully**:
   - Implement exponential backoff in clients
   - Check `retryAfter` in 429 responses
   - Use queuing for batch operations

3. **Monitor retry attempts**:
   - Log all retry events
   - Alert on excessive retry patterns
   - Track failure rates by service (Claude, OpenAI, Qdrant)

4. **Test resilience regularly**:
   - Simulate network failures
   - Test timeout scenarios
   - Validate retry behavior

---

**Related Documentation**:
- [Phase 1 Hardening Details](PHASE1-HARDENING.md)
- [Main README](README.md)
