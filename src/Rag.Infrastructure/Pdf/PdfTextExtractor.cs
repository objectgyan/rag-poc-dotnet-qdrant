using Rag.Core.Services;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Rag.Infrastructure.Pdf;

public sealed class PdfTextExtractor : IPdfTextExtractor
{
    public Task<List<PdfPageText>> ExtractTextAsync(Stream pdfStream)
    {
        var pages = new List<PdfPageText>();

        using var document = PdfDocument.Open(pdfStream);
        
        foreach (Page page in document.GetPages())
        {
            var text = page.Text;
            
            // Skip empty pages
            if (string.IsNullOrWhiteSpace(text))
                continue;
            
            pages.Add(new PdfPageText(page.Number, text));
        }

        return Task.FromResult(pages);
    }
}
