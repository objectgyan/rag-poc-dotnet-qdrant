using FluentValidation;
using Rag.Api.Models;

namespace Rag.Api.Validation;

/// <summary>
/// Validator for IngestCodebaseRequest to ensure codebase ingestion parameters are valid.
/// </summary>
public class IngestCodebaseRequestValidator : AbstractValidator<IngestCodebaseRequest>
{
    private const int MinChunkSize = 100;
    private const int MaxChunkSize = 5000;

    public IngestCodebaseRequestValidator()
    {
        RuleFor(x => x.DirectoryPath)
            .NotEmpty()
            .WithMessage("DirectoryPath cannot be empty")
            .Must(BeValidDirectoryPath)
            .WithMessage("DirectoryPath contains invalid characters");

        RuleFor(x => x.ChunkSize)
            .InclusiveBetween(MinChunkSize, MaxChunkSize)
            .WithMessage($"ChunkSize must be between {MinChunkSize} and {MaxChunkSize}");

        RuleFor(x => x.IncludePatterns)
            .Must(patterns => patterns == null || patterns.Count <= 50)
            .WithMessage("Cannot have more than 50 include patterns");

        RuleFor(x => x.ExcludePatterns)
            .Must(patterns => patterns == null || patterns.Count <= 50)
            .WithMessage("Cannot have more than 50 exclude patterns");
    }

    private static bool BeValidDirectoryPath(string path)
    {
        // Basic validation - check for common path traversal patterns
        if (string.IsNullOrWhiteSpace(path))
            return false;

        // Disallow path traversal attempts
        if (path.Contains("..") || path.Contains("~"))
            return false;

        // Check for invalid path characters
        var invalidChars = System.IO.Path.GetInvalidPathChars();
        return !path.Any(c => invalidChars.Contains(c));
    }
}
