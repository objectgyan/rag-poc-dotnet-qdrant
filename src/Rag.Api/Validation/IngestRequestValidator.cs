using FluentValidation;
using Rag.Api.Models;

namespace Rag.Api.Validation;

/// <summary>
/// Validator for IngestRequest to ensure documents are valid before ingestion.
/// </summary>
public class IngestRequestValidator : AbstractValidator<IngestRequest>
{
    private const int MaxDocumentIdLength = 255;
    private const int MinTextLength = 10;
    private const int MaxTextLength = 1_000_000; // 1 million characters

    public IngestRequestValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty()
            .WithMessage("DocumentId cannot be empty")
            .MaximumLength(MaxDocumentIdLength)
            .WithMessage($"DocumentId cannot exceed {MaxDocumentIdLength} characters")
            .Must(BeValidDocumentId)
            .WithMessage("DocumentId must contain only alphanumeric characters, hyphens, and underscores");

        RuleFor(x => x.Text)
            .NotEmpty()
            .WithMessage("Text cannot be empty")
            .MinimumLength(MinTextLength)
            .WithMessage($"Text must be at least {MinTextLength} characters long")
            .MaximumLength(MaxTextLength)
            .WithMessage($"Text cannot exceed {MaxTextLength} characters");
    }

    private static bool BeValidDocumentId(string documentId)
    {
        // Only allow alphanumeric, hyphens, and underscores
        return documentId.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
    }
}
