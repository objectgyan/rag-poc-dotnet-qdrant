using System.Text;
using System.Text.RegularExpressions;

namespace Rag.Core.Text;

/// <summary>
/// PHASE 6: Enhanced prompt injection guards to protect against malicious inputs.
/// Detects and sanitizes common prompt injection patterns.
/// </summary>
public static class PromptGuards
{
    // 🔐 PHASE 6: Enhanced injection patterns
    private static readonly Regex[] InjectionPatterns = new[]
    {
        // Role manipulation
        new Regex(@"(?i)\b(ignore|disregard|forget)\b.*\b(instructions|system|previous|prior)\b", RegexOptions.Compiled),
        new Regex(@"(?i)\byou are (now |chatgpt|gpt-|claude|an? ai|a language model)", RegexOptions.Compiled),
        new Regex(@"(?i)\b(pretend|act as|roleplay as|you're now)\b.*\b(admin|developer|system)\b", RegexOptions.Compiled),

        // Instruction override
        new Regex(@"(?i)\b(system prompt|your instructions|your rules):\s*", RegexOptions.Compiled),
        new Regex(@"(?i)BEGIN (SYSTEM|INSTRUCTIONS|ADMIN|ROOT).*?END \1", RegexOptions.Compiled | RegexOptions.Singleline),
        new Regex(@"(?i)\[SYSTEM\]|\[ADMIN\]|\[ROOT\]|\[OVERRIDE\]", RegexOptions.Compiled),

        // Developer mode / jailbreak attempts
        new Regex(@"(?i)\b(developer mode|debug mode|god mode|admin mode)\b", RegexOptions.Compiled),
        new Regex(@"(?i)\benable (developer|debug|admin|god) mode\b", RegexOptions.Compiled),
        new Regex(@"(?i)\bdo anything now|DAN\b", RegexOptions.Compiled),

        // Prompt extraction
        new Regex(@"(?i)\b(repeat|print|show|display|output|return)\b.*\b(prompt|instructions|system message)\b", RegexOptions.Compiled),
        new Regex(@"(?i)\bwhat (is|are) your (instructions|system prompt|rules)", RegexOptions.Compiled),
    };

    // 🔐 PHASE 6: Suspicious patterns (for detection/logging)
    private static readonly Regex[] SuspiciousPatterns = new[]
    {
        // Base64 encoded payloads (potential obfuscation)
        new Regex(@"(?:[A-Za-z0-9+/]{40,}={0,2})", RegexOptions.Compiled),

        // Unicode homoglyph attacks (look-alike characters)
        new Regex(@"[\u0400-\u04FF]", RegexOptions.Compiled), // Cyrillic
        new Regex(@"[\u0370-\u03FF]", RegexOptions.Compiled), // Greek

        // Excessive special characters (potential encoding tricks)
        new Regex(@"[^\w\s]{20,}", RegexOptions.Compiled),
    };

    /// <summary>
    /// Sanitizes context from retrieved documents to remove prompt injection attempts.
    /// </summary>
    public static string SanitizeContext(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        var sanitized = text;

        // Apply all injection pattern filters
        foreach (var pattern in InjectionPatterns)
        {
            sanitized = pattern.Replace(sanitized, "[removed]");
        }

        return sanitized.Trim();
    }

    /// <summary>
    /// Detects suspicious patterns in user input (returns true if suspicious).
    /// Use this for logging/monitoring, not blocking (to avoid false positives).
    /// </summary>
    public static bool ContainsSuspiciousPatterns(string text, out List<string> detectedPatterns)
    {
        detectedPatterns = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
            return false;

        // Check injection patterns
        foreach (var pattern in InjectionPatterns)
        {
            if (pattern.IsMatch(text))
            {
                detectedPatterns.Add($"Injection pattern: {pattern}");
            }
        }

        // Check suspicious patterns
        foreach (var pattern in SuspiciousPatterns)
        {
            if (pattern.IsMatch(text))
            {
                detectedPatterns.Add($"Suspicious pattern: {pattern}");
            }
        }

        return detectedPatterns.Count > 0;
    }

    /// <summary>
    /// Validates that text doesn't exceed length limits to prevent resource exhaustion.
    /// </summary>
    public static bool IsWithinLengthLimits(string text, int maxLength = 10_000)
    {
        return !string.IsNullOrEmpty(text) && text.Length <= maxLength;
    }

    /// <summary>
    /// Removes excessive whitespace and normalizes line endings.
    /// </summary>
    public static string NormalizeWhitespace(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Normalize line endings
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        // Remove excessive consecutive whitespace
        text = Regex.Replace(text, @"[ \t]+", " ", RegexOptions.Compiled);
        text = Regex.Replace(text, @"\n{3,}", "\n\n", RegexOptions.Compiled);

        return text.Trim();
    }
}