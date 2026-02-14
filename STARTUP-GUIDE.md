# RAG POC - Complete Startup Guide

This guide shows you how to run the complete RAG system with both backend API and frontend UI.

## Prerequisites

### Backend Requirements
- .NET 10 SDK
- Docker Desktop (for Qdrant vector database)
- API keys:
  - OpenAI API key (for embeddings)
  - Anthropic API key (for Claude chat)
  - (Optional) GitHub token for agent code search

### Frontend Requirements
- Node.js 18+ and npm
- Modern web browser (Chrome, Firefox, Safari, Edge)

## Quick Start

### Step 1: Start Qdrant Vector Database

```bash
docker run -d -p 6333:6333 -v qdrant_storage:/qdrant/storage qdrant/qdrant
```

Verify Qdrant is running: http://localhost:6333/dashboard

### Step 2: Configure Backend API

Edit `src/Rag.Api/appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "OpenAI": {
    "ApiKey": "your-openai-api-key-here"
  },
  "Claude": {
    "ApiKey": "your-anthropic-api-key-here"
  },
  "Qdrant": {
    "Host": "localhost",
    "Port": 6333
  }
}
```

### Step 3: Start Backend API

Open Terminal 1:

```bash
cd d:\Mayank\Learning\RAG\rag-poc-dotnet-qdrant
dotnet run --project src/Rag.Api/Rag.Api.csproj
```

The API will start on: http://localhost:5129

Verify API is running:
- Swagger UI: http://localhost:5129/swagger
- Health check: http://localhost:5129/health (if configured)

### Step 4: Configure Frontend

Edit `src/Rag.Web/.env`:

```env
VITE_API_URL=http://localhost:5129
VITE_API_KEY=secure_password
```

Note: The `VITE_API_KEY` should match the API key configured in your backend.

### Step 5: Install Frontend Dependencies (First Time Only)

Open Terminal 2:

```bash
cd d:\Mayank\Learning\RAG\rag-poc-dotnet-qdrant\src\Rag.Web
npm install
```

### Step 6: Start Frontend

In the same Terminal 2:

```bash
npm run dev
```

The frontend will start on: http://localhost:3000

## Using the Application

### 1. Open the Frontend

Navigate to http://localhost:3000 in your web browser.

### 2. Upload a Document

1. Click the paperclip icon (📎) in the chat input area
2. Drag and drop a PDF or text file
3. Set a document ID (auto-filled from filename)
4. Click "Upload Document"
5. Wait for upload to complete

### 3. Ask Questions (RAG Mode)

1. Ensure RAG mode is active (FileText icon in sidebar)
2. Type your question about the uploaded documents
3. Press Enter or click Send
4. View the answer with citations showing source documents

### 4. Use Agent Mode

1. Toggle to Agent mode (Bot icon in sidebar)
2. Ask complex questions that require reasoning
3. View tool usage (document search, web search, etc.)
4. See cost and performance metrics

### 5. Multi-Tenant Features

1. Click the workspace button in the sidebar
2. Add a new workspace with a custom name
3. Switch between workspaces
4. Each workspace has isolated conversations and documents

## Troubleshooting

### Backend Issues

**Error: Qdrant connection failed**
```bash
# Check if Docker is running
docker ps

# Check if Qdrant container is running
docker ps | grep qdrant

# Restart Qdrant if needed
docker restart <qdrant-container-id>
```

**Error: OpenAI API key invalid**
- Verify your API key in `appsettings.Development.json`
- Check key has proper permissions
- Verify you have credits/balance

**Error: Port 5129 already in use**
```bash
# Windows: Find process using port
netstat -ano | findstr :5129

# Kill the process
taskkill /PID <process-id> /F
```

### Frontend Issues

**Error: Cannot connect to API**
- Verify backend is running on http://localhost:5129
- Check `.env` file has correct `VITE_API_URL`
- Check browser console for CORS errors
- Restart frontend dev server

**Error: Module not found**
```bash
# Delete node_modules and reinstall
cd src/Rag.Web
rm -rf node_modules
rm package-lock.json
npm install
```

**Error: Port 3000 already in use**
```bash
# Windows: Find and kill process
netstat -ano | findstr :3000
taskkill /PID <process-id> /F

# Or change port in vite.config.ts
```

### Docker Issues

**Qdrant won't start**
```bash
# Check Docker daemon is running
docker info

# Pull latest Qdrant image
docker pull qdrant/qdrant:latest

# Remove old container and start fresh
docker rm -f <qdrant-container-id>
docker run -d -p 6333:6333 -v qdrant_storage:/qdrant/storage qdrant/qdrant
```

## Development Workflow

### Running Tests

Backend tests:
```bash
dotnet test
```

Frontend tests (if configured):
```bash
cd src/Rag.Web
npm test
```

### Building for Production

Backend:
```bash
dotnet publish src/Rag.Api/Rag.Api.csproj -c Release -o ./publish
```

Frontend:
```bash
cd src/Rag.Web
npm run build
# Output will be in src/Rag.Web/dist/
```

### Viewing Logs

Backend logs are in console output and Application Insights (if configured).

Frontend logs are in browser console (F12 → Console).

## Environment Variables Reference

### Backend (appsettings.Development.json)

| Variable | Description | Required | Example |
|----------|-------------|----------|---------|
| `OpenAI:ApiKey` | OpenAI API key for embeddings | Yes | `sk-proj-...` |
| `Claude:ApiKey` | Anthropic API key for chat | Yes | `sk-ant-...` |
| `Qdrant:Host` | Qdrant host address | Yes | `localhost` |
| `Qdrant:Port` | Qdrant port number | Yes | `6333` |
| `GitHub:Token` | GitHub token for code search | No | `ghp_...` |

### Frontend (.env)

| Variable | Description | Required | Example |
|----------|-------------|----------|---------|
| `VITE_API_URL` | Backend API base URL | Yes | `http://localhost:5129` |
| `VITE_API_KEY` | API key for authentication | Yes | `secure_password` |

## System Architecture

```
┌─────────────────┐         ┌─────────────────┐
│   Frontend      │         │   Backend API   │
│  (React/Vite)   │◄───────►│   (.NET 10)     │
│  Port 3000      │  HTTP   │   Port 5129     │
└─────────────────┘         └─────────────────┘
                                     │
                                     │
                    ┌────────────────┴────────────────┐
                    │                                  │
             ┌──────▼──────┐                  ┌───────▼────────┐
             │   Qdrant    │                  │  OpenAI/Claude │
             │  (Docker)   │                  │      APIs      │
             │  Port 6333  │                  │    (Cloud)     │
             └─────────────┘                  └────────────────┘
```

## Useful Commands

### Check Service Status

```powershell
# Backend API
curl http://localhost:5129/swagger

# Frontend
curl http://localhost:3000

# Qdrant
curl http://localhost:6333/collections
```

### Stop All Services

```bash
# Stop backend (Ctrl+C in Terminal 1)
# Stop frontend (Ctrl+C in Terminal 2)

# Stop Qdrant
docker stop <qdrant-container-id>
```

### Restart Everything

```bash
# Terminal 1
cd d:\Mayank\Learning\RAG\rag-poc-dotnet-qdrant
dotnet run --project src/Rag.Api/Rag.Api.csproj

# Terminal 2
cd d:\Mayank\Learning\RAG\rag-poc-dotnet-qdrant\frontend
npm run dev
```

## Next Steps

1. **Upload Documents**: Start by uploading your first PDF or text file
2. **Test RAG**: Ask questions about your documents
3. **Try Agent Mode**: Use the AI agent for complex queries
4. **Create Workspaces**: Add multiple tenants to organize your work
5. **Evaluate**: Use the evaluation endpoints to measure quality
6. **Monitor**: Check metrics for cost and performance

## Support

- Backend documentation: See [README.md](README.md)
- Frontend documentation: See [src/Rag.Web/README.md](src/Rag.Web/README.md)
- API documentation: http://localhost:5129/swagger
- Integration guide: See [FRONTEND-INTEGRATION-GUIDE.md](FRONTEND-INTEGRATION-GUIDE.md)

---

**Happy RAG'ing! 🚀**
