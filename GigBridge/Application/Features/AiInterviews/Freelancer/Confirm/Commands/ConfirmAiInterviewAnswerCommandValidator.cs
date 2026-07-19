using FluentValidation;

namespace Application.Features.AiInterviews.Freelancer.Confirm.Commands;

public sealed class ConfirmAiInterviewAnswerCommandValidator : AbstractValidator<ConfirmAiInterviewAnswerCommand>
{
    public ConfirmAiInterviewAnswerCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.SessionId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.CorrectedText).MaximumLength(10000);
    }
}
