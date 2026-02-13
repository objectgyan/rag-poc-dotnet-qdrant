# 📂 PHASE 3: Real-World Features - PDF, Background Jobs & Document Management

## Overview

This phase adds essential production features for handling real-world document workflows:

- **PDF Ingestion**: Extract text from PDF files with page number preservation
- **Background Job Processing**: Non-blocking ingestion using Hangfire queue
- **Document Management**: Delete and update documents with full CRUD operations
- **Job Monitoring**: Hangfire dashboard for tracking background jobs

## 🎯 Features Implemented

### 1. PDF Text Extraction
- **Library**: UglyToad.PdfPig for robust PDF parsing
- **Page Metadata**: Preserves page numbers during extraction
- **Smart Chunking**: Chunks PDF pages intelligently with page context
- **Error Handling**: Gracefully handles malformed PDFs

### 2. Background Job Processing
- **Queue System**: Hangfire with in-memory storage (development)
- **Non-Blocking**: API returns immediately with job ID
- **Scalable**: Configurable worker count for concurrent processing
- **Monitoring**: Built-in dashboard at `/hangfire`

### 3. Document CRUD Operations
- **Upload PDF**: POST `/documents/upload-pdf` - Multipart form upload
- **Update Document**: PUT `/documents/{documentId}` - Replace existing document
- **Delete Document**: DELETE `/documents/{documentId}` - Remove all chunks
- **Synchronous Ingest**: POST `/ingest` - For small text documents

## 🏗️ Architecture

### Request Flow: PDF Upload

```
1. Client uploads PDF to POST /documents/upload-pdf
2. DocumentController receives file, validates
3. File bytes copied to memory (Hangfire serialization)
4. Background job queued, Job ID returned immediately
5. API responds with 200 OK + Job ID
6. Hangfire worker picks up job
7. Worker extracts text from PDF (page by page)
8. Text chunked with page metadata
9. Chunks embedded and stored in Qdrant
10. Job completes (monitor in /hangfire dashboard)
```

### Request Flow: Delete Document

```
1. Client calls DELETE /documents/{documentId}
2. DocumentController extracts tenant ID from context
3. QdrantVectorStore.DeleteByDocumentIdAsync called
4. Qdrant filter: { must: [{ documentId: "..." }, { tenantId: "..." }] }
5. All matching vectors deleted
6. Response: 200 OK with confirmation
```

## 📊 New API Endpoints

### 1. Upload PDF (Background)

```http
POST /documents/upload-pdf
Content-Type: multipart/form-data
X-API-Key: secure_password
X-Tenant-Id: acme-corp

--boundary
Content-Disposition: form-data; name="file"; filename="report.pdf"
Content-Type: application/pdf

[PDF binary data]
--boundary
Content-Disposition: form-data; name="documentId"

financial-report-2026
--boundary--
```

**Response**:
```json
{
  "jobId": "1a2b3c4d",
  "documentId": "financial-report-2026",
  "status": "queued",
  "message": "PDF ingestion job queued. Job ID: 1a2b3c4d"
}
```

### 2. Delete Document

```http
DELETE /documents/financial-report-2026
X-API-Key: secure_password
X-Tenant-Id: acme-corp
```

**Response**:
```json
{
  "documentId": "financial-report-2026",
  "tenantId": "acme-corp",
  "message": "Document financial-report-2026 deleted successfully"
}
```

### 3. Update Document (Background)

```http
PUT /documents/product-guide
Content-Type: application/json
X-API-Key: secure_password
X-Tenant-Id: acme-corp

{
  "documentId": "product-guide",
  "text": "Updated product documentation..."
}
```

**Response**:
```json
{
  "jobId": "5e6f7g8h",
  "documentId": "product-guide",
  "status": "queued",
  "message": "Document update job queued. Job ID: 5e6f7g8h"
}
```

### 4. Synchronous Text Ingest (Legacy)

```http
POST /ingest
Content-Type: application/json
X-API-Key: secure_password

{
  "documentId": "quick-note",
  "text": "This is a short text document that can be ingested synchronously."
}
```

**Response**:
```json
{
  "documentId": "quick-note",
  "chunkCount": 1,
  "tenantId": "default"
}
```

## 🔧 Configuration

### Hangfire Settings (Program.cs)

```csharp
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseMemoryStorage()); // For production: Use SQL Server or Redis

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 2; // Adjust based on server capacity
});
```

### Production Storage

For production, replace `UseMemoryStorage()` with persistent storage:

#### SQL Server
```csharp
builder.Services.AddHangfire(config => config
    .UseSqlServerStorage(connectionString));
```

#### Redis
```csharp
builder.Services.AddHangfire(config => config
    .UseRedisStorage(connectionString));
```

## 📈 Hangfire Dashboard

Access the Hangfire dashboard at: **http://localhost:5129/hangfire**

### Dashboard Features:
- **Jobs**: View queued, processing, succeeded, and failed jobs
- **Recurring Jobs**: Schedule periodic tasks
- **Servers**: Monitor Hangfire worker servers
- **Retries**: Automatic retry of failed jobs
- **Real-time Updates**: Live job status monitoring

### Security Note
⚠️ In production, **secure the Hangfire dashboard**:

```csharp
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

// Custom authorization filter
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // Only allow admins
        var httpContext = context.GetHttpContext();
        return httpContext.User.IsInRole("Admin");
    }
}
```

## 🧪 Testing Guide

### Test 1: Upload PDF

```bash
# Create a sample PDF or use an existing one
curl -X POST http://localhost:5129/documents/upload-pdf \
  -H "X-API-Key: secure_password" \
  -H "X-Tenant-Id: test-tenant" \
  -F "file=@sample.pdf" \
  -F "documentId=test-doc-1"

# Response includes Job ID
# {
#   "jobId": "abc123",
#   "documentId": "test-doc-1",
#   "status": "queued",
#   "message": "PDF ingestion job queued. Job ID: abc123"
# }
```

### Test 2: Monitor Job Progress

1. Open browser: `http://localhost:5129/hangfire`
2. Navigate to "Jobs" → "Processing" or "Succeeded"
3. Find your job by ID (e.g., "abc123")
4. View logs and execution details

### Test 3: Query Ingested PDF

```bash
# Wait for job to complete, then query
curl -X POST http://localhost:5129/ask \
  -H "Content-Type: application/json" \
  -H "X-API-Key: secure_password" \
  -H "X-Tenant-Id: test-tenant" \
  -d '{
    "question": "What does the PDF say about revenue?",
    "topK": 3
  }'

# Response includes citations with page numbers
# {
#   "answer": "According to page 5, revenue was $10M...",
#   "citations": [
#     { "documentId": "test-doc-1", "chunkIndex": 12, "score": 0.89 }
#   ],
#   "tenantId": "test-tenant"
# }
```

### Test 4: Delete Document

```bash
curl -X DELETE http://localhost:5129/documents/test-doc-1 \
  -H "X-API-Key: secure_password" \
  -H "X-Tenant-Id: test-tenant"

# Response
# {
#   "documentId": "test-doc-1",
#   "tenantId": "test-tenant",
#   "message": "Document test-doc-1 deleted successfully"
# }

# Verify deletion - query should return no results
curl -X POST http://localhost:5129/ask \
  -H "Content-Type: application/json" \
  -H "X-API-Key: secure_password" \
  -H "X-Tenant-Id: test-tenant" \
  -d '{
    "question": "What does test-doc-1 say?",
    "topK": 3
  }'
```

### Test 5: Update Document

```bash
# Update with new content
curl -X PUT http://localhost:5129/documents/test-doc-1 \
  -H "Content-Type: application/json" \
  -H "X-API-Key: secure_password" \
  -H "X-Tenant-Id: test-tenant" \
  -d '{
    "documentId": "test-doc-1",
    "text": "This is the updated version of the document with new information."
  }'

# Response includes Job ID for update job
# {
#   "jobId": "xyz789",
#   "documentId": "test-doc-1",
#   "status": "queued",
#   "message": "Document update job queued. Job ID: xyz789"
# }
```

## 🔍 Chunk Metadata Structure

### Text Document Chunks

```json
{
  "id": "uuid-generated-from-tenantid:docid:chunkindex",
  "vector": [0.123, 0.456, ...],
  "payload": {
    "documentId": "my-doc",
    "chunkIndex": 0,
    "text": "Chunk text content...",
    "tenantId": "acme-corp"
  }
}
```

### PDF Document Chunks (with Page Numbers)

```json
{
  "id": "uuid-generated-from-tenantid:docid:chunkindex",
  "vector": [0.123, 0.456, ...],
  "payload": {
    "documentId": "financial-report",
    "chunkIndex": 5,
    "text": "Quarter 4 revenue was $10M...",
    "pageNumber": 12,
    "tenantId": "acme-corp"
  }
}
```

## 💡 Best Practices

### PDF Processing

1. **File Size Limits**: Set max file size in Kestrel configuration
   ```json
   {
     "Kestrel": {
       "Limits": {
         "MaxRequestBodySize": 52428800  // 50 MB
       }
     }
   }
   ```

2. **Timeout Configuration**: Adjust for large PDFs
   ```csharp
   builder.Services.Configure<FormOptions>(options =>
   {
       options.MultipartBodyLengthLimit = 52428800; // 50 MB
   });
   ```

3. **PDF Validation**: Verify file type before processing
   - Check Content-Type header
   - Validate file extension
   - Inspect PDF magic bytes (header)

### Background Jobs

1. **Job Persistence**: Use SQL/Redis in production
2. **Worker Scaling**: Adjust worker count based on CPU cores
3. **Job Retry**: Hangfire auto-retries failed jobs
4. **Job Timeout**: Set reasonable timeout for large documents
5. **Dead Letter Queue**: Monitor and handle permanently failed jobs

### Document Management

1. **Delete Strategy**: Consider soft deletes with metadata flag
2. **Update Pattern**: Delete + ingest ensures consistency
3. **Versioning**: Add version field to track document revisions
4. **Audit Trail**: Log all document operations for compliance

## 🚀 Performance Considerations

### Ingestion Throughput

- **Synchronous**: ~2-5 docs/sec (blocks API thread)
- **Background**: ~10-20 docs/sec (depends on workers)
- **Batch Processing**: Group similar documents for efficiency

### Optimization Strategies

1. **Parallel Embeddings**: Process chunks in parallel
   ```csharp
   var embeddingTasks = chunks.Select(c => _embeddings.EmbedAsync(c, ct));
   var results = await Task.WhenAll(embeddingTasks);
   ```

2. **Batch Upsert**: Send multiple vectors in one request
3. **Worker Scaling**: Increase worker count for high load
4. **Caching**: Cache embeddings for identical chunks

## 📊 Monitoring & Observability

### Key Metrics to Track

1. **Job Queue Length**: Monitor backlog
2. **Job Processing Time**: Avg time per document
3. **Job Failure Rate**: % of failed jobs
4. **PDF Page Count**: Track document complexity
5. **Chunk Count per Document**: Monitor chunking efficiency

### Logging

Jobs automatically log:
```
[Information] Starting PDF ingestion for document {DocumentId}, tenant {TenantId}
[Information] Extracted {PageCount} pages from PDF {DocumentId}
[Information] Completed PDF ingestion for document {DocumentId}, chunks={ChunkCount}, duration={DurationMs}ms
```

## ⚠️ Troubleshooting

### Issue: Job Stays in "Processing" State

**Cause**: Worker crashed or job timeout exceeded

**Solution**:
1. Check Hangfire dashboard for error details
2. Review application logs
3. Increase job timeout if needed
4. Restart Hangfire server

### Issue: PDF Extraction Fails

**Cause**: Corrupted or encrypted PDF

**Solution**:
1. Validate PDF file integrity
2. Check for password protection
3. Handle extraction errors gracefully
4. Log specific PDF parsing errors

### Issue: Delete Doesn't Remove All Chunks

**Cause**: Tenant ID mismatch or filter issue

**Solution**:
1. Verify tenant ID in request
2. Check Qdrant filter syntax
3. Use Qdrant dashboard to inspect data
4. Add logging to delete operation

## 🔐 Security Considerations

### PDF Upload Security

1. **File Type Validation**: Strictly validate PDF MIME type
2. **File Size Limits**: Prevent DoS with large files
3. **Virus Scanning**: Integrate antivirus for uploaded files
4. **Content Filtering**: Scan for malicious content

### Hangfire Dashboard

1. **Authentication**: Require admin role
2. **HTTPS Only**: Never expose dashboard over HTTP
3. **IP Whitelisting**: Restrict access to internal IPs
4. **Read-Only Mode**: Limit user permissions

## 📚 Code Examples

### Custom PDF Processing

```csharp
public class MyCustomPdfExtractor : IPdfTextExtractor
{
    public async Task<List<PdfPageText>> ExtractTextAsync(Stream pdfStream)
    {
        var pages = new List<PdfPageText>();
        
        using var document = PdfDocument.Open(pdfStream);
        
        foreach (var page in document.GetPages())
        {
            // Custom extraction logic
            var text = ExtractWithCustomRules(page);
            
            // Add metadata
            pages.Add(new PdfPageText(page.Number, text));
        }
        
        return pages;
    }
}
```

### Background Job with Progress Updates

```csharp
[NonAction]
public async Task ProcessLargeDocumentWithProgress(
    string documentId, 
    byte[] data, 
    string? tenantId, 
    CancellationToken ct)
{
    var totalPages = GetPageCount(data);
    
    for (int i = 0; i < totalPages; i++)
    {
        // Update job progress
        BackgroundJob.SetProgress(PerformContext.BackgroundJob.Id, 
            new { processed = i + 1, total = totalPages });
        
        // Process page
        await ProcessPage(i, data, tenantId, ct);
    }
}
```

## ✅ Summary

Phase 3 Real-World Features transform your RAG API into a **production-ready document management system**:

- ✅ **PDF Ingestion** with page-level metadata preservation
- ✅ **Background Job Processing** for non-blocking operations
- ✅ **Document CRUD** with delete, update, and upload
- ✅ **Job Monitoring** via Hangfire dashboard
- ✅ **Smart Chunking** for PDFs with context preservation
- ✅ **Tenant Isolation** for multi-tenant document management

**Production Readiness Checklist**:
- [ ] Replace MemoryStorage with SQL/Redis
- [ ] Add Hangfire dashboard authentication
- [ ] Implement file size and type validation
- [ ] Configure job timeouts and retry policies
- [ ] Add virus scanning for uploaded files
- [ ] Set up monitoring and alerting
- [ ] Implement soft deletes with audit trail
- [ ] Add document versioning

**Next Steps**:
1. Test PDF upload with real documents
2. Monitor job processing in Hangfire dashboard
3. Implement batch document upload
4. Add document metadata search
5. Optimize chunking for specific document types
