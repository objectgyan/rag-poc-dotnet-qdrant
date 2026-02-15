using Microsoft.Extensions.Logging;
using Rag.Core.Models;
using Rag.Core.Services;
using System.Drawing;
using System.Text;
using Tesseract;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Graphics;

namespace Rag.Infrastructure.Pdf;

public sealed class PdfTextExtractor : IPdfTextExtractor
{
    private readonly ILogger<PdfTextExtractor> _logger;
    private readonly PdfSettings _pdfSettings;

    public PdfTextExtractor(ILogger<PdfTextExtractor> logger, PdfSettings pdfSettings)
    {
        _logger = logger;
        _pdfSettings = pdfSettings;
    }

    public async Task<List<PdfPageText>> ExtractTextAsync(Stream pdfStream)
    {
        var pages = new List<PdfPageText>();

        try
        {
            // Save stream position for potential OCR retry
            var canSeek = pdfStream.CanSeek;
            long originalPosition = canSeek ? pdfStream.Position : 0;

            using var document = PdfDocument.Open(pdfStream);
            
            var totalPages = document.NumberOfPages;
            var emptyPageCount = 0;
            
            _logger.LogInformation("PDF contains {TotalPages} pages", totalPages);
            
            // First attempt: Standard text extraction
            foreach (UglyToad.PdfPig.Content.Page page in document.GetPages())
            {
                var text = page.Text;
                
                if (string.IsNullOrWhiteSpace(text))
                {
                    emptyPageCount++;
                    _logger.LogDebug("Page {PageNumber} is empty or contains no extractable text", page.Number);
                    continue;
                }
                
                _logger.LogDebug("Page {PageNumber} extracted {CharCount} characters", page.Number, text.Length);
                pages.Add(new PdfPageText(page.Number, text));
            }

            // If all pages are empty and OCR is enabled, try OCR
            if (emptyPageCount == totalPages && totalPages > 0 && _pdfSettings.EnableOcr)
            {
                _logger.LogWarning(
                    "All {TotalPages} pages are empty. PDF appears to be image-based. Attempting OCR extraction...",
                    totalPages);

                // Reset stream if possible
                if (canSeek)
                {
                    pdfStream.Position = originalPosition;
                }

                pages = await ExtractTextWithOcrAsync(pdfStream);
                
                if (pages.Count > 0)
                {
                    _logger.LogInformation("OCR extraction successful: extracted text from {PageCount} pages", pages.Count);
                }
                else
                {
                    _logger.LogWarning("OCR extraction failed: no text could be extracted from image-based PDF");
                }
            }
            else if (emptyPageCount > 0 && emptyPageCount < totalPages)
            {
                _logger.LogInformation(
                    "Skipped {EmptyPages} empty pages out of {TotalPages} total pages",
                    emptyPageCount, totalPages);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from PDF");
            throw;
        }

        return pages;
    }

    private async Task<List<PdfPageText>> ExtractTextWithOcrAsync(Stream pdfStream)
    {
        var pages = new List<PdfPageText>();

        try
        {
            if (!Directory.Exists(_pdfSettings.TessdataPath))
            {
                _logger.LogError("Tessdata path not found: {Path}", _pdfSettings.TessdataPath);
                return pages;
            }

            using var engine = new TesseractEngine(_pdfSettings.TessdataPath, _pdfSettings.OcrLanguage, EngineMode.Default);
            using var document = PdfDocument.Open(pdfStream);
            
            _logger.LogInformation("Starting OCR on {PageCount} pages", document.NumberOfPages);

            foreach (UglyToad.PdfPig.Content.Page page in document.GetPages())
            {
                try
                {
                    // Get page images
                    var images = page.GetImages().ToList();
                    
                    if (images.Count == 0)
                    {
                        _logger.LogDebug("Page {PageNumber} has no images to OCR", page.Number);
                        continue;
                    }

                    var pageText = new StringBuilder();
                    
                    // OCR each image on the page
                    foreach (var image in images)
                    {
                        try
                        {
                            var rawBytes = image.RawBytes.ToArray();
                            
                            if (rawBytes.Length == 0)
                                continue;

                            // Try to process the image with Tesseract
                            using var memStream = new MemoryStream(rawBytes);
                            try
                            {
                                using var img = System.Drawing.Image.FromStream(memStream);
                                using var bitmap = new Bitmap(img);
                                
                                // Convert Bitmap to Pix using Tesseract's Pix.LoadFromMemory
                                using var bmpStream = new MemoryStream();
                                bitmap.Save(bmpStream, System.Drawing.Imaging.ImageFormat.Png);
                                bmpStream.Position = 0;
                                
                                using var pix = Pix.LoadFromMemory(bmpStream.ToArray());
                                using var tessPage = engine.Process(pix);
                                
                                var text = tessPage.GetText();
                                if (!string.IsNullOrWhiteSpace(text))
                                {
                                    pageText.AppendLine(text);
                                }
                            }
                            catch
                            {
                                // Image format not supported, skip
                                continue;
                            }
                        }
                        catch (Exception imgEx)
                        {
                            _logger.LogDebug(imgEx, "Failed to OCR image on page {PageNumber}", page.Number);
                        }
                    }

                    var extractedText = pageText.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(extractedText))
                    {
                        pages.Add(new PdfPageText(page.Number, extractedText));
                        _logger.LogDebug("OCR Page {PageNumber}: extracted {CharCount} characters", 
                            page.Number, extractedText.Length);
                    }
                }
                catch (Exception pageEx)
                {
                    _logger.LogWarning(pageEx, "Failed to OCR page {PageNumber}", page.Number);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCR extraction failed");
        }

        return pages;
    }
}
