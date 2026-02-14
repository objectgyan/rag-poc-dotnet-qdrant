# Rag.Tests - Integration Test Suite

## Overview

Comprehensive integration tests for the RAG API using xUnit and ASP.NET Core's `WebApplicationFactory`.

## Quick Start

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run specific test class
dotnet test --filter "FullyQualifiedName~DocumentApiTests"
```

## Test Structure

### Test Classes

1. **IntegrationTestBase** - Base class with shared setup
   - Configures WebApplicationFactory
   - Provides pre-configured HttpClient
   - Helper methods for common operations

2. **DocumentApiTests** (10 tests)
   - PDF upload functionality
   - Document deletion
   - Document updates
   - Multi-tenancy isolation
   - Authentication

3. **RagQueryTests** (8 tests)
   - Text ingestion
   - RAG queries (ask endpoint)
   - Multi-tenant query isolation
   - End-to-end workflows

### Test Helpers

- **TestHelpers.CreateTestPdf()** - Generates valid PDF files in memory
- **GenerateTestDocumentId()** - Creates unique document IDs
- **CreateClientWithHeaders()** - Custom HTTP client with headers

## Test Configuration

Tests use the same configuration as the main API:
- `appsettings.json` from Rag.Api project
- Environment variables (if set)
- Default test headers (X-API-Key, X-Tenant-Id)

### Important Note

**These are integration tests** that require:
- ✅ **NO external dependencies** (uses in-memory Hangfire storage)
- ❌ **Qdrant** - Tests will call real Qdrant instance (if configured)
- ❌ **OpenAI** - Tests will make real API calls (if configured)

To run tests without external dependencies, you would need to:
1. Mock `IVectorStore` and `IEmbeddingModel`
2. Or use test doubles
3. Or configure test-specific services

## Test Categories

### Authentication Tests
- API key validation
- Unauthorized access handling
- Tenant ID verification

### Document Management Tests
- PDF upload (background jobs)
- Document deletion (tenant-aware)
- Document updates (delete + re-ingest)
- Concurrent operations

### RAG Workflow Tests
- Text ingestion
- Query with context retrieval
- Citation generation
- Answer generation

### Multi-Tenancy Tests
- Tenant data isolation
- Cross-tenant query prevention
- Same document ID, different tenants

## Common Test Patterns

### Test a Protected Endpoint

```csharp
[Fact]
public async Task Endpoint_WithValidAuth_ReturnsSuccess()
{
    // Client is pre-configured with API key
    var response = await Client.PostAsJsonAsync("/endpoint", data);
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

### Test Multi-Tenancy

```csharp
[Fact]
public async Task Feature_WithDifferentTenants_AreIsolated()
{
    var tenant1Client = CreateClientWithHeaders(TestApiKey, "tenant1");
    var tenant2Client = CreateClientWithHeaders(TestApiKey, "tenant2");
    
    // Perform operations
    // Verify isolation
}
```

### Test Background Jobs

```csharp
[Fact]
public async Task UploadPdf_QueuesBackgroundJob()
{
    var response = await Client.PostAsync("/documents/upload-pdf", content);
    var result = await response.Content.ReadFromJsonAsync<IngestJobResponse>();
    
    result.JobId.Should().NotBeNullOrEmpty();
    result.Status.Should().Be("queued");
    
    // Note: In real tests, wait for job completion or mock the job queue
}
```

## Known Issues

### Hangfire ObjectDisposedException

When tests complete, Hangfire workers may try to process jobs after the application context is disposed, resulting in warnings like:

```
System.ObjectDisposedException: Cannot access a disposed object.
Object name: 'IServiceProvider'.
```

**This is expected behavior** and does not affect test results. The tests themselves pass successfully.

**To fix** (if needed):
1. Disable Hangfire server in tests
2. Mock `IBackgroundJobClient`
3. Use a separate test configuration

## Debugging Tests

### Visual Studio
1. Set breakpoint in test method
2. Right-click test → Debug Tests
3. Step through code

### VS Code
1. Add launch configuration:
```json
{
    "name": ".NET Core Test",
    "type": "coreclr",
    "request": "launch",
    "program": "dotnet",
    "args": ["test"],
    "cwd": "${workspaceFolder}/src/Rag.Tests"
}
```
2. Set breakpoint and press F5

## Extending Tests

### Add a New Test

```csharp
[Fact]
public async Task MyNewFeature_Scenario_ExpectedResult()
{
    // Arrange
    var data = ...;
    
    // Act
    var response = await Client.PostAsJsonAsync("/endpoint", data);
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

### Add Test Data

Extend `TestHelpers` class:

```csharp
public static class TestHelpers
{
    public static MyTestData CreateTestData()
    {
        return new MyTestData { ... };
    }
}
```

## CI/CD Integration

Tests are designed to run in CI/CD pipelines:

```yaml
- name: Test
  run: dotnet test --no-build --verbosity normal
```

## Performance

- **Test execution time**: ~1 minute (18 tests)
- **Memory usage**: ~200MB (includes Hangfire, test data)
- **Concurrency**: Tests run in parallel by default

## Best Practices

1. **Test Independence**: Each test should be isolated
2. **Unique IDs**: Use `GenerateTestDocumentId()` to avoid conflicts
3. **Clean Up**: Delete test data after use
4. **Async Delays**: Account for background job processing time
5. **Assertions**: Use FluentAssertions for readable tests

## Next Steps

1. Add unit tests for individual components
2. Mock external services (Qdrant, OpenAI)
3. Add performance/load tests
4. Implement test data builders
5. Add contract tests for API schemas
