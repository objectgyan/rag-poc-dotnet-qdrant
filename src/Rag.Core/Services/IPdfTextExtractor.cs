namespace Rag.Core.Services;

/// <summary>
/// Service for extracting text from PDF files with page metadata.
/// </summary>
public interface IPdfTextExtractor
{
    /// <summary>
    /// Extracts text from a PDF file, preserving page numbers.
    /// </summary>
    /// <param name="pdfStream">Stream containing PDF data</param>
    /// <returns>List of page texts with page numbers</returns>
    Task<List<PdfPageText>> ExtractTextAsync(Stream pdfStream);
}

/// <summary>
/// Represents text extracted from a single PDF page.
/// </summary>
public sealed record PdfPageText(int PageNumber, string Text);
