using System.Text;

namespace Rag.Tests;

public static class TestHelpers
{
    /// <summary>
    /// Creates a simple PDF file in memory for testing purposes.
    /// This creates a minimal valid PDF with some text content.
    /// </summary>
    public static byte[] CreateTestPdf(string content = "This is a test PDF document for RAG testing.")
    {
        var sb = new StringBuilder();
        
        // PDF Header
        sb.AppendLine("%PDF-1.4");
        
        // Catalog Object
        sb.AppendLine("1 0 obj");
        sb.AppendLine("<<");
        sb.AppendLine("/Type /Catalog");
        sb.AppendLine("/Pages 2 0 R");
        sb.AppendLine(">>");
        sb.AppendLine("endobj");
        
        // Pages Object
        sb.AppendLine("2 0 obj");
        sb.AppendLine("<<");
        sb.AppendLine("/Type /Pages");
        sb.AppendLine("/Kids [3 0 R]");
        sb.AppendLine("/Count 1");
        sb.AppendLine(">>");
        sb.AppendLine("endobj");
        
        // Page Object
        sb.AppendLine("3 0 obj");
        sb.AppendLine("<<");
        sb.AppendLine("/Type /Page");
        sb.AppendLine("/Parent 2 0 R");
        sb.AppendLine("/Resources <<");
        sb.AppendLine("/Font <<");
        sb.AppendLine("/F1 <<");
        sb.AppendLine("/Type /Font");
        sb.AppendLine("/Subtype /Type1");
        sb.AppendLine("/BaseFont /Helvetica");
        sb.AppendLine(">>");
        sb.AppendLine(">>");
        sb.AppendLine(">>");
        sb.AppendLine("/MediaBox [0 0 612 792]");
        sb.AppendLine("/Contents 4 0 R");
        sb.AppendLine(">>");
        sb.AppendLine("endobj");
        
        // Content Stream
        var streamContent = $"BT\n/F1 12 Tf\n50 700 Td\n({content}) Tj\nET\n";
        sb.AppendLine("4 0 obj");
        sb.AppendLine("<<");
        sb.AppendLine($"/Length {streamContent.Length}");
        sb.AppendLine(">>");
        sb.AppendLine("stream");
        sb.Append(streamContent);
        sb.AppendLine("endstream");
        sb.AppendLine("endobj");
        
        // Cross-reference Table
        sb.AppendLine("xref");
        sb.AppendLine("0 5");
        sb.AppendLine("0000000000 65535 f ");
        sb.AppendLine("0000000009 00000 n ");
        sb.AppendLine("0000000074 00000 n ");
        sb.AppendLine("0000000133 00000 n ");
        sb.AppendLine("0000000340 00000 n ");
        
        // Trailer
        sb.AppendLine("trailer");
        sb.AppendLine("<<");
        sb.AppendLine("/Size 5");
        sb.AppendLine("/Root 1 0 R");
        sb.AppendLine(">>");
        sb.AppendLine("startxref");
        sb.AppendLine("445");
        sb.AppendLine("%%EOF");
        
        return Encoding.ASCII.GetBytes(sb.ToString());
    }
    
    /// <summary>
    /// Creates a multi-page test PDF
    /// </summary>
    public static byte[] CreateMultiPageTestPdf()
    {
        // For simplicity, we'll just create a PDF with repeated content
        // In real scenarios, you'd use a proper PDF library like PdfSharp or iTextSharp
        var content = "Page 1: Introduction to RAG. Page 2: Vector embeddings explained. Page 3: Retrieval strategies.";
        return CreateTestPdf(content);
    }
}
