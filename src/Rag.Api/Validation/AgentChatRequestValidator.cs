using FluentValidation;
using Rag.Api.Models;

namespace Rag.Api.Validation;

/// <summary>
/// Validator for AgentChatRequest to ensure agent messages are valid and safe.
/// </summary>
public class AgentChatRequestValidator : AbstractValidator<AgentChatRequest>
{
    private const int MinMessageLength = 1;
    private const int MaxMessageLength = 2000;
    private const int MaxConversationHistoryLength = 50;
    private const int MaxToolCallsLimit = 10;

    public AgentChatRequestValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty()
            .WithMessage("Message cannot be empty")
            .MinimumLength(MinMessageLength)
            .WithMessage($"Message must be at least {MinMessageLength} character long")
            .MaximumLength(MaxMessageLength)
            .WithMessage($"Message cannot exceed {MaxMessageLength} characters");

        RuleFor(x => x.ConversationHistory)
            .Must(history => history == null || history.Count <= MaxConversationHistoryLength)
            .WithMessage($"Conversation history cannot exceed {MaxConversationHistoryLength} messages");

        When(x => x.Config != null, () =>
        {
            RuleFor(x => x.Config!.MaxToolCalls)
                .InclusiveBetween(1, MaxToolCallsLimit)
                .WithMessage($"MaxToolCalls must be between 1 and {MaxToolCallsLimit}");

            RuleFor(x => x.Config!.TopKDocuments)
                .InclusiveBetween(1, 20)
                .WithMessage("TopKDocuments must be between 1 and 20");

            RuleFor(x => x.Config!.MinRelevanceScore)
                .InclusiveBetween(0.0, 1.0)
                .WithMessage("MinRelevanceScore must be between 0.0 and 1.0");

            RuleFor(x => x.Config!.SystemPrompt)
                .MaximumLength(1000)
                .When(x => !string.IsNullOrEmpty(x.Config!.SystemPrompt))
                .WithMessage("SystemPrompt cannot exceed 1000 characters");
        });
    }
}
