using System.Text.RegularExpressions;

namespace Rag.Core.Text;

public static class PromptGuards
{
    // Minimal sanitation: remove obvious instruction-y markers from retrieved text
    public static string SanitizeContext(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        // Remove common jailbreak phrases (keep conservative; don’t destroy content)
        var t = text;

        t = Regex.Replace(t, @"(?i)\b(ignore|disregard)\b.*\b(instructions|system|previous)\b", "[removed]", RegexOptions.Singleline);
        t = Regex.Replace(t, @"(?i)\byou are chatgpt\b.*", "[removed]", RegexOptions.Singleline);

        // Remove tool-like patterns (optional)
        t = Regex.Replace(t, @"(?i)BEGIN (SYSTEM|INSTRUCTIONS).*?END \1", "[removed]", RegexOptions.Singleline);

        return t.Trim();
    }
}