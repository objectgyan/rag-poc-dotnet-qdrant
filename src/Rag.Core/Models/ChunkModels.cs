namespace Rag.Core.Models;

/// <summary>
/// Represents a text chunk with optional metadata.
/// </summary>
public sealed class TextChunk
{
    public string Text { get; set; } = "";
    public int? PageNumber { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
