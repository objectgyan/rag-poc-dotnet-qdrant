namespace Rag.Core.Text;

public static class Chunker
{
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
}