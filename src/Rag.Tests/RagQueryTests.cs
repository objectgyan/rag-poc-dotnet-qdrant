using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rag.Tests;

/// <summary>
/// Integration tests for RAG query endpoints (/ingest and /ask)
/// </summary>
public class RagQueryTests : IntegrationTestBase
{
    public RagQueryTests(WebApplicationFactory<Program> factory) : base(factory) { }
    
    [Fact]
    public async Task IngestText_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var documentId = GenerateTestDocumentId();
        var request = new
        {
            documentId,
            text = "Qdrant is a vector database that enables similarity search. It supports high-dimensional vectors and metadata filtering."
        };
        
        // Act
        var response = await Client.PostAsJsonAsync("/ingest", request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("documentId").GetString().Should().Be(documentId);
        result.GetProperty("chunkCount").GetInt32().Should().BeGreaterThan(0);
        result.GetProperty("tenantId").GetString().Should().Be(TestTenantId);
    }
    
    [Fact]
    public async Task IngestText_WithoutApiKey_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateClientWithHeaders(apiKey: null, tenantId: TestTenantId);
        var request = new { documentId = "test", text = "test content" };
        
        // Act
        var response = await client.PostAsJsonAsync("/ingest", request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    
    [Fact]
    public async Task IngestText_WithEmptyText_ReturnsBadRequest()
    {
        // Arrange
        var request = new { documentId = "test", text = "" };
        
        // Act
        var response = await Client.PostAsJsonAsync("/ingest", request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task AskQuestion_WithIngestedData_ReturnsAnswer()
    {
        // Arrange - First ingest some data
        var documentId = GenerateTestDocumentId();
        var ingestRequest = new
        {
            documentId,
            text = "The capital of France is Paris. Paris is known for the Eiffel Tower and the Louvre Museum. It has a population of over 2 million people."
        };
        await Client.PostAsJsonAsync("/ingest", ingestRequest);
        
        // Wait for ingestion to complete
        await Task.Delay(2000);
        
        // Act - Ask a question
        var askRequest = new
        {
            question = "What is the capital of France?",
            topK = 3
        };
        var response = await Client.PostAsJsonAsync("/ask", askRequest);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("answer").GetString().Should().NotBeNullOrEmpty();
        result.GetProperty("tenantId").GetString().Should().Be(TestTenantId);
        
        // Should have citations
        var citations = result.GetProperty("citations");
        citations.GetArrayLength().Should().BeGreaterThan(0);
    }
    
    [Fact]
    public async Task AskQuestion_WithoutApiKey_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateClientWithHeaders(apiKey: null, tenantId: TestTenantId);
        var request = new { question = "test question", topK = 3 };
        
        // Act
        var response = await client.PostAsJsonAsync("/ask", request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    
    [Fact]
    public async Task AskQuestion_WithEmptyQuestion_ReturnsBadRequest()
    {
        // Arrange
        var request = new { question = "", topK = 3 };
        
        // Act
        var response = await Client.PostAsJsonAsync("/ask", request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task AskQuestion_DifferentTenants_GetIsolatedResults()
    {
        // Arrange - Ingest data for tenant1
        var tenant1Client = CreateClientWithHeaders(TestApiKey, "tenant1");
        var tenant1Doc = new
        {
            documentId = "tenant1-doc",
            text = "Tenant 1 secret: The password is ALPHA123"
        };
        await tenant1Client.PostAsJsonAsync("/ingest", tenant1Doc);
        
        // Ingest data for tenant2
        var tenant2Client = CreateClientWithHeaders(TestApiKey, "tenant2");
        var tenant2Doc = new
        {
            documentId = "tenant2-doc",
            text = "Tenant 2 secret: The password is BETA456"
        };
        await tenant2Client.PostAsJsonAsync("/ingest", tenant2Doc);
        
        await Task.Delay(2000);
        
        // Act - Tenant 1 asks about their data
        var tenant1Question = new { question = "What is the password?", topK = 3 };
        var tenant1Response = await tenant1Client.PostAsJsonAsync("/ask", tenant1Question);
        var tenant1Result = await tenant1Response.Content.ReadFromJsonAsync<JsonElement>();
        
        // Tenant 2 asks about their data
        var tenant2Response = await tenant2Client.PostAsJsonAsync("/ask", tenant1Question);
        var tenant2Result = await tenant2Response.Content.ReadFromJsonAsync<JsonElement>();
        
        // Assert - Each tenant should only see their own data
        var tenant1Answer = tenant1Result.GetProperty("answer").GetString();
        var tenant2Answer = tenant2Result.GetProperty("answer").GetString();
        
        tenant1Answer.Should().Contain("ALPHA123");
        tenant1Answer.Should().NotContain("BETA456");
        
        tenant2Answer.Should().Contain("BETA456");
        tenant2Answer.Should().NotContain("ALPHA123");
    }
    
    [Fact]
    public async Task IngestAndQuery_CompleteWorkflow_Works()
    {
        // Arrange
        var documentId = GenerateTestDocumentId();
        
        // Step 1: Ingest document
        var ingestRequest = new
        {
            documentId,
            text = @"Vector databases are specialized systems for storing and querying high-dimensional vectors. 
                     They enable semantic search by finding similar vectors using distance metrics like cosine similarity.
                     Popular vector databases include Qdrant, Pinecone, and Weaviate."
        };
        var ingestResponse = await Client.PostAsJsonAsync("/ingest", ingestRequest);
        ingestResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        await Task.Delay(2000);
        
        // Step 2: Query with relevant question
        var askRequest = new { question = "What are vector databases used for?", topK = 3 };
        var askResponse = await Client.PostAsJsonAsync("/ask", askRequest);
        askResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await askResponse.Content.ReadFromJsonAsync<JsonElement>();
        var answer = result.GetProperty("answer").GetString();
        
        // Step 3: Verify answer contains relevant information
        answer.Should().NotBeNullOrEmpty();
        // The answer should mention something about vectors or search
        (answer.Contains("vector", StringComparison.OrdinalIgnoreCase) ||
         answer.Contains("search", StringComparison.OrdinalIgnoreCase) ||
         answer.Contains("similar", StringComparison.OrdinalIgnoreCase))
         .Should().BeTrue();
        
        // Step 4: Delete document
        var deleteResponse = await Client.DeleteAsync($"/documents/{documentId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
