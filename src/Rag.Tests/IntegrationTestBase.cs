using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Headers;

namespace Rag.Tests;

/// <summary>
/// Base class for integration tests that provides a configured HttpClient
/// </summary>
public class IntegrationTestBase : IClassFixture<WebApplicationFactory<Program>>
{
    protected readonly HttpClient Client;
    protected readonly WebApplicationFactory<Program> Factory;
    
    protected const string TestApiKey = "secure_password";
    protected const string TestTenantId = "test-tenant";
    
    public IntegrationTestBase(WebApplicationFactory<Program> factory)
    {
        Factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        
        // Set default headers for all requests
        Client.DefaultRequestHeaders.Add("X-API-Key", TestApiKey);
        Client.DefaultRequestHeaders.Add("X-Tenant-Id", TestTenantId);
    }
    
    /// <summary>
    /// Creates a new HttpClient with custom headers
    /// </summary>
    protected HttpClient CreateClientWithHeaders(string? apiKey = null, string? tenantId = null)
    {
        var client = Factory.CreateClient();
        
        if (apiKey != null)
        {
            client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        }
        
        if (tenantId != null)
        {
            client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        }
        
        return client;
    }
    
    /// <summary>
    /// Generates a unique document ID for testing
    /// </summary>
    protected static string GenerateTestDocumentId() => $"test-doc-{Guid.NewGuid():N}";
    
    /// <summary>
    /// Creates multipart form data content for file upload
    /// </summary>
    protected static MultipartFormDataContent CreatePdfUploadContent(byte[] pdfBytes, string documentId)
    {
        var content = new MultipartFormDataContent();
        
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        content.Add(fileContent, "file", "test.pdf");
        
        content.Add(new StringContent(documentId), "documentId");
        
        return content;
    }
}
