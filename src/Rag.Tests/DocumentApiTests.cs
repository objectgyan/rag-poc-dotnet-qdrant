using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rag.Tests;

/// <summary>
/// Integration tests for Document Management APIs (PDF upload, delete, update)
/// </summary>
public class DocumentApiTests : IntegrationTestBase
{
    public DocumentApiTests(WebApplicationFactory<Program> factory) : base(factory) { }
    
    [Fact]
    public async Task UploadPdf_WithValidPdf_ReturnsJobId()
    {
        // Arrange
        var pdfBytes = TestHelpers.CreateTestPdf("Sample financial report Q4 2025.");
        var documentId = GenerateTestDocumentId();
        var content = CreatePdfUploadContent(pdfBytes, documentId);
        
        // Act
        var response = await Client.PostAsync("/documents/upload-pdf", content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("jobId").GetString().Should().NotBeNullOrEmpty();
        result.GetProperty("documentId").GetString().Should().Be(documentId);
        result.GetProperty("status").GetString().Should().Be("queued");
    }
    
    [Fact]
    public async Task UploadPdf_WithoutApiKey_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateClientWithHeaders(apiKey: null, tenantId: TestTenantId);
        var pdfBytes = TestHelpers.CreateTestPdf();
        var documentId = GenerateTestDocumentId();
        var content = CreatePdfUploadContent(pdfBytes, documentId);
        
        // Act
        var response = await client.PostAsync("/documents/upload-pdf", content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    
    [Fact]
    public async Task UploadPdf_WithoutFile_ReturnsBadRequest()
    {
        // Arrange
        var content = new MultipartFormDataContent();
        content.Add(new StringContent("test-doc"), "documentId");
        
        // Act
        var response = await Client.PostAsync("/documents/upload-pdf", content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task DeleteDocument_WithValidId_ReturnsSuccess()
    {
        // Arrange - First upload a document
        var pdfBytes = TestHelpers.CreateTestPdf("Document to be deleted.");
        var documentId = GenerateTestDocumentId();
        var uploadContent = CreatePdfUploadContent(pdfBytes, documentId);
        await Client.PostAsync("/documents/upload-pdf", uploadContent);
        
        // Wait a moment for ingestion (in real tests, you'd wait for job completion)
        await Task.Delay(2000);
        
        // Act - Delete the document
        var response = await Client.DeleteAsync($"/documents/{documentId}");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("documentId").GetString().Should().Be(documentId);
        result.GetProperty("tenantId").GetString().Should().Be(TestTenantId);
    }
    
    [Fact]
    public async Task DeleteDocument_WithoutApiKey_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateClientWithHeaders(apiKey: null, tenantId: TestTenantId);
        
        // Act
        var response = await client.DeleteAsync("/documents/test-doc");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    
    [Fact]
    public async Task UpdateDocument_WithValidData_ReturnsJobId()
    {
        // Arrange
        var documentId = GenerateTestDocumentId();
        var updateRequest = new
        {
            documentId,
            text = "Updated document content with new information about vector databases."
        };
        
        // Act
        var response = await Client.PutAsJsonAsync($"/documents/{documentId}", updateRequest);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("jobId").GetString().Should().NotBeNullOrEmpty();
        result.GetProperty("documentId").GetString().Should().Be(documentId);
        result.GetProperty("status").GetString().Should().Be("queued");
    }
    
    [Fact]
    public async Task UploadPdf_WithMultipleFiles_AllReturnDifferentJobIds()
    {
        // Arrange
        var pdf1 = TestHelpers.CreateTestPdf("First document about AI.");
        var pdf2 = TestHelpers.CreateTestPdf("Second document about ML.");
        var doc1Id = GenerateTestDocumentId();
        var doc2Id = GenerateTestDocumentId();
        
        // Act
        var response1 = await Client.PostAsync("/documents/upload-pdf", 
            CreatePdfUploadContent(pdf1, doc1Id));
        var response2 = await Client.PostAsync("/documents/upload-pdf", 
            CreatePdfUploadContent(pdf2, doc2Id));
        
        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result1 = await response1.Content.ReadFromJsonAsync<JsonElement>();
        var result2 = await response2.Content.ReadFromJsonAsync<JsonElement>();
        
        var jobId1 = result1.GetProperty("jobId").GetString();
        var jobId2 = result2.GetProperty("jobId").GetString();
        
        jobId1.Should().NotBe(jobId2);
    }
    
    [Fact]
    public async Task UploadPdf_WithDifferentTenants_AreIsolated()
    {
        // Arrange
        var tenant1Client = CreateClientWithHeaders(TestApiKey, "tenant1");
        var tenant2Client = CreateClientWithHeaders(TestApiKey, "tenant2");
        
        var pdfBytes = TestHelpers.CreateTestPdf("Multi-tenant test document.");
        var documentId = "shared-doc-name"; // Same doc ID, different tenants
        
        // Act
        var response1 = await tenant1Client.PostAsync("/documents/upload-pdf", 
            CreatePdfUploadContent(pdfBytes, documentId));
        var response2 = await tenant2Client.PostAsync("/documents/upload-pdf", 
            CreatePdfUploadContent(pdfBytes, documentId));
        
        // Assert - Both should succeed (tenant isolation)
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result1 = await response1.Content.ReadFromJsonAsync<JsonElement>();
        var result2 = await response2.Content.ReadFromJsonAsync<JsonElement>();
        
        // Different tenants should get different job IDs even with same doc ID
        result1.GetProperty("jobId").GetString()
            .Should().NotBe(result2.GetProperty("jobId").GetString());
    }
}
