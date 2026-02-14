using FluentValidation;
using Rag.Api.Models;

namespace Rag.Api.Validation;

/// <summary>
/// Validator for AskRequest to ensure questions are valid and safe.
/// </summary>
public class AskRequestValidator : AbstractValidator<AskRequest>
{
    private const int MinQuestionLength = 3;
    private const int MaxQuestionLength = 500;
    private const int MinTopK = 1;
    private const int MaxTopK = 20;

    public AskRequestValidator()
    {
        RuleFor(x => x.Question)
            .NotEmpty()
            .WithMessage("Question cannot be empty")
            .MinimumLength(MinQuestionLength)
            .WithMessage($"Question must be at least {MinQuestionLength} characters long")
            .MaximumLength(MaxQuestionLength)
            .WithMessage($"Question cannot exceed {MaxQuestionLength} characters")
            .Must(NotContainControlCharacters)
            .WithMessage("Question contains invalid control characters");

        RuleFor(x => x.TopK)
            .InclusiveBetween(MinTopK, MaxTopK)
            .WithMessage($"TopK must be between {MinTopK} and {MaxTopK}");
    }

    private static bool NotContainControlCharacters(string question)
    {
        // Check for control characters (except newline, carriage return, and tab)
        return !question.Any(c => char.IsControl(c) && c != '\n' && c != '\r' && c != '\t');
    }
}
