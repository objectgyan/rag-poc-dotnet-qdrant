# 🧪 Testing Guide - RAG API

## Overview

This guide covers automated and manual testing approaches for the RAG API, including integration tests, manual HTTP tests, and end-to-end workflows.

## 📁 Test Structure

```
src/Rag.Tests/
├── IntegrationTestBase.cs      # Base class with WebApplicationFactory setup
├── DocumentApiTests.cs         # PDF upload, delete, update tests
├── RagQueryTests.cs           # Ingest and query endpoint tests
└── TestHelpers.cs             # PDF generation and utility methods

tests.http                      # Manual HTTP test file for VS Code/Visual Studio
```

## 🚀 Running Automated Tests

### Run All Tests

```bash
# From repository root
dotnet test src/Rag.Tests/Rag.Tests.csproj

# With detailed output
dotnet test src/Rag.Tests/Rag.Tests.csproj --logger "console;verbosity=detailed"

# With code coverage
dotnet test src/Rag.Tests/Rag.Tests.csproj --collect:"XPlat Code Coverage"
```

### Run Specific Test Class

```bash
# Run only DocumentApiTests
dotnet test src/Rag.Tests/Rag.Tests.csproj --filter "FullyQualifiedName~DocumentApiTests"

# Run only RagQueryTests
dotnet test src/Rag.Tests/Rag.Tests.csproj --filter "FullyQualifiedName~RagQueryTests"
```

### Run Specific Test Method

```bash
# Run a single test
dotnet test src/Rag.Tests/Rag.Tests.csproj --filter "FullyQualifiedName~UploadPdf_WithValidPdf_ReturnsJobId"
```

## 📊 Test Coverage

### DocumentApiTests (10 tests)

1. **UploadPdf_WithValidPdf_ReturnsJobId**
   - Uploads a valid PDF and verifies job ID is returned
   - Checks response status and structure

2. **UploadPdf_WithoutApiKey_ReturnsUnauthorized**
   - Tests API key authentication
   - Expects 401 Unauthorized

3. **UploadPdf_WithoutFile_ReturnsBadRequest**
   - Tests validation for missing file
   - Expects 400 Bad Request

4. **DeleteDocument_WithValidId_ReturnsSuccess**
   - Uploads a document, then deletes it
   - Verifies successful deletion with tenant isolation

5. **DeleteDocument_WithoutApiKey_ReturnsUnauthorized**
   - Tests delete endpoint authentication
   - Expects 401 Unauthorized

6. **UpdateDocument_WithValidData_ReturnsJobId**
   - Updates existing document with new content
   - Verifies background job is queued

7. **UploadPdf_WithMultipleFiles_AllReturnDifferentJobIds**
   - Tests concurrent PDF uploads
   - Ensures unique job IDs

8. **UploadPdf_WithDifferentTenants_AreIsolated**
   - Tests multi-tenant isolation
   - Same document ID, different tenants should work

### RagQueryTests (8 tests)

1. **IngestText_WithValidData_ReturnsSuccess**
   - Tests synchronous text ingestion
   - Verifies chunk count and tenant ID

2. **IngestText_WithoutApiKey_ReturnsUnauthorized**
   - Tests ingest endpoint authentication
   - Expects 401 Unauthorized

3. **IngestText_WithEmptyText_ReturnsBadRequest**
   - Tests input validation
   - Expects 400 Bad Request

4. **AskQuestion_WithIngestedData_ReturnsAnswer**
   - End-to-end RAG workflow
   - Ingest → Query → Verify answer and citations

5. **AskQuestion_WithoutApiKey_ReturnsUnauthorized**
   - Tests query endpoint authentication
   - Expects 401 Unauthorized

6. **AskQuestion_WithEmptyQuestion_ReturnsBadRequest**
   - Tests query validation
   - Expects 400 Bad Request

7. **AskQuestion_DifferentTenants_GetIsolatedResults**
   - **Critical multi-tenancy test**
   - Verifies tenants cannot see each other's data
   - Ingests different secrets for each tenant
   - Confirms tenant isolation in query results

8. **IngestAndQuery_CompleteWorkflow_Works**
   - Full lifecycle test: Ingest → Query → Delete
   - Tests document management workflow

## 🔧 Manual Testing with tests.http

### Prerequisites

- **VS Code**: Install [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension
- **Visual Studio**: Built-in HTTP file support (.http files)
- **Command Line**: Use `curl` commands provided in the file

### Using tests.http

1. **Open** `tests.http` in VS Code or Visual Studio
2. **Update variables** at the top if needed:
   ```http
   @baseUrl = http://localhost:5129
   @apiKey = secure_password
   @tenantId = test-tenant
   ```
3. **Click "Send Request"** above any `###` separator
4. **View response** in the side panel

### Test Workflow

```
1. Start API: dotnet run --project src/Rag.Api
2. Open Hangfire dashboard: http://localhost:5129/hangfire
3. Run tests from tests.http file
4. Monitor background jobs in dashboard
5. Verify results in HTTP response panel
```

## 🧩 Test Components

### IntegrationTestBase

Base class that provides:
- **WebApplicationFactory**: In-memory test server
- **Configured HttpClient**: Pre-configured with auth headers
- **Helper methods**: Document ID generation, PDF upload content creation
- **Multi-tenant support**: Easy tenant switching

### TestHelpers

Utility class that provides:
- **CreateTestPdf()**: Generates a minimal valid PDF in memory
- **CreateMultiPageTestPdf()**: Creates PDF with multiple pages

**Why in-memory PDFs?**
- No external file dependencies
- Fast test execution
- Consistent test data
- Easy to modify content per test

### Test Configuration

Tests use the same configuration as the main API:
- `appsettings.json` from Rag.Api
- `appsettings.Development.json` (if exists)
- Can be overridden using environment variables

## 🎯 Testing Best Practices

### 1. Test Independence

Each test is self-contained:
```csharp
// Generate unique document IDs to avoid conflicts
var documentId = GenerateTestDocumentId(); // Returns "test-doc-{guid}"
```

### 2. Async Delays

Background jobs need time to process:
```csharp
await Client.PostAsync("/documents/upload-pdf", content);
await Task.Delay(2000); // Wait for job processing
await Client.PostAsync("/ask", query);
```

**Note**: In production tests, use proper job completion polling instead of fixed delays.

### 3. Multi-Tenancy Testing

Always test tenant isolation:
```csharp
var tenant1Client = CreateClientWithHeaders(TestApiKey, "tenant1");
var tenant2Client = CreateClientWithHeaders(TestApiKey, "tenant2");

// Verify tenant1 cannot see tenant2's data
```

### 4. Test Data Cleanup

Tests should clean up after themselves:
```csharp
// Create document
await Client.PostAsync("/ingest", data);

// Test functionality
var response = await Client.PostAsync("/ask", query);

// Clean up
await Client.DeleteAsync($"/documents/{documentId}");
```

## 📈 Continuous Integration

### GitHub Actions Example

```yaml
name: Run Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      
      - name: Restore dependencies
        run: dotnet restore
      
      - name: Build
        run: dotnet build --no-restore
      
      - name: Test
        run: dotnet test --no-build --verbosity normal --collect:"XPlat Code Coverage"
      
      - name: Upload coverage
        uses: codecov/codecov-action@v3
```

## 🐛 Troubleshooting

### Issue: Tests Timeout

**Cause**: Background jobs taking longer than expected

**Solution**:
1. Increase delay in tests: `await Task.Delay(5000);`
2. Use Hangfire dashboard to check job status
3. Consider using polling for job completion:
   ```csharp
   for (int i = 0; i < 10; i++)
   {
       // Check if job completed
       await Task.Delay(1000);
   }
   ```

### Issue: PDF Extraction Fails in Tests

**Cause**: Invalid PDF structure in TestHelpers.CreateTestPdf()

**Solution**:
- Use a real PDF file for testing (less portable)
- Or use a proper PDF library like PdfSharp to generate test PDFs
- Check PdfPig logs for extraction errors

### Issue: Tests Pass Locally but Fail in CI

**Cause**: Different configuration or missing services (Qdrant, OpenAI)

**Solution**:
1. Mock external services in tests
2. Use test doubles for IEmbeddingModel and IVectorStore
3. Configure test-specific appsettings
4. Set environment variables in CI

### Issue: Multi-Tenancy Tests Fail

**Cause**: Tenant context not properly isolated

**Solution**:
1. Verify X-Tenant-Id header is set
2. Check TenantContext middleware is registered
3. Ensure each test uses unique document IDs
4. Wait for previous ingestion to complete before querying

## 🔐 Testing Security Features

### API Key Authentication

```csharp
[Fact]
public async Task Endpoint_WithoutApiKey_ReturnsUnauthorized()
{
    var client = CreateClientWithHeaders(apiKey: null);
    var response = await client.PostAsJsonAsync("/ingest", data);
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
```

### JWT Authentication (Phase 3A)

```csharp
[Fact]
public async Task Endpoint_WithValidJwt_ReturnsSuccess()
{
    var jwt = GenerateTestJwt("user@test.com", "Premium");
    var client = CreateClientWithHeaders();
    client.DefaultRequestHeaders.Authorization = 
        new AuthenticationHeaderValue("Bearer", jwt);
    
    var response = await client.PostAsJsonAsync("/ingest", data);
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

### Rate Limiting (Phase 1)

```csharp
[Fact]
public async Task Endpoint_ExceedsRateLimit_ReturnsTooManyRequests()
{
    // Send 100 requests rapidly
    for (int i = 0; i < 100; i++)
    {
        await Client.PostAsJsonAsync("/ingest", smallData);
    }
    
    // Next request should be rate limited
    var response = await Client.PostAsJsonAsync("/ingest", data);
    response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
}
```

## 📝 Adding New Tests

### Step 1: Create Test Class

```csharp
public class MyNewTests : IntegrationTestBase
{
    public MyNewTests(WebApplicationFactory<Program> factory) 
        : base(factory) { }
    
    [Fact]
    public async Task MyNewFeature_WithValidInput_ReturnsSuccess()
    {
        // Arrange
        var data = new { ... };
        
        // Act
        var response = await Client.PostAsJsonAsync("/my-endpoint", data);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

### Step 2: Run and Debug

```bash
# Run your new test
dotnet test --filter "FullyQualifiedName~MyNewFeature"

# Debug in Visual Studio or VS Code
# Set breakpoint in test, press F5
```

### Step 3: Add to CI Pipeline

Tests are automatically discovered and run by `dotnet test`.

## 🎓 Test-Driven Development (TDD)

### TDD Workflow

1. **Write failing test** for new feature
2. **Run test** - should fail (RED)
3. **Implement feature** - minimal code to pass
4. **Run test** - should pass (GREEN)
5. **Refactor** - improve code quality
6. **Run test** - should still pass

### Example: Adding Document Search

```csharp
// 1. Write failing test
[Fact]
public async Task SearchDocuments_WithKeyword_ReturnsMatchingDocs()
{
    // Arrange
    await IngestTestDocument("doc1", "AI and machine learning");
    await IngestTestDocument("doc2", "Database optimization");
    
    // Act
    var response = await Client.GetAsync("/documents/search?q=AI");
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var results = await response.Content.ReadFromJsonAsync<SearchResult[]>();
    results.Should().ContainSingle(r => r.DocumentId == "doc1");
}

// 2. Run test - FAILS (endpoint doesn't exist)
// 3. Implement SearchDocuments endpoint
// 4. Run test - PASSES
// 5. Refactor if needed
```

## ✅ Summary

### Automated Tests
- **18 integration tests** covering all major features
- **WebApplicationFactory** for in-memory testing
- **FluentAssertions** for readable test assertions
- **xUnit** test framework

### Manual Testing
- **tests.http** file for VS Code/Visual Studio
- **Hangfire dashboard** for job monitoring
- **curl commands** for command-line testing

### Test Coverage
- ✅ PDF upload (background jobs)
- ✅ Document CRUD (create, delete, update)
- ✅ RAG queries (ingest, ask)
- ✅ Multi-tenancy isolation
- ✅ Authentication (API key)
- ✅ Input validation
- ✅ Error handling

### Next Steps
1. Run tests: `dotnet test src/Rag.Tests/Rag.Tests.csproj`
2. Review test results and fix any failures
3. Add tests for Phase 3A features (JWT, cost tracking)
4. Set up CI/CD pipeline with automated testing
5. Add performance tests for high-load scenarios
