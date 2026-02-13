using Rag.Core.Models;
using Rag.Core.Services;

namespace Rag.Core.Text;

public static class Chunker
{
    /// <summary>
    /// Chunks plain text without metadata.
    /// </summary>
    public static IReadOnlyList<string> Chunk(string text, int size = 900, int overlap = 150, int maxChunks = 200)
    {
        text = (text ?? "")
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();

        if (text.Length == 0) return Array.Empty<string>();
        if (text.Length <= size) return new[] { text };

        var chunks = new List<string>();
        for (int i = 0; i < text.Length && chunks.Count < maxChunks;)
        {
            int len = Math.Min(size, text.Length - i);
            chunks.Add(text.Substring(i, len));
            i += Math.Max(1, size - overlap);
        }

        return chunks;
    }

    /// <summary>
    /// Chunks PDF pages, preserving page metadata.
    /// </summary>
    public static IReadOnlyList<TextChunk> ChunkPdfPages(
        List<PdfPageText> pages, 
        int size = 900, 
        int overlap = 150, 
        int maxChunksPerPage = 50)
    {
        var allChunks = new List<TextChunk>();

        foreach (var page in pages)
        {
            var pageText = (page.Text ?? "")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();

            if (string.IsNullOrWhiteSpace(pageText))
                continue;

            // If page fits in one chunk
            if (pageText.Length <= size)
            {
                allChunks.Add(new TextChunk
                {
                    Text = pageText,
                    PageNumber = page.PageNumber
                });
                continue;
            }

            // Split page into multiple chunks
            var pageChunks = new List<string>();
            for (int i = 0; i < pageText.Length && pageChunks.Count < maxChunksPerPage; )
            {
                int len = Math.Min(size, pageText.Length - i);
                pageChunks.Add(pageText.Substring(i, len));
                i += Math.Max(1, size - overlap);
            }

            // Add all chunks with page metadata
            foreach (var chunkText in pageChunks)
            {
                allChunks.Add(new TextChunk
                {
                    Text = chunkText,
                    PageNumber = page.PageNumber
                });
            }
        }

        return allChunks;
    }
}