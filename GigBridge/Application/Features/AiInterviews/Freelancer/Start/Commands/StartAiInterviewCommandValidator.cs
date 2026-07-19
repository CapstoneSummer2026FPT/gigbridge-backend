using FluentValidation;

namespace Application.Features.AiInterviews.Freelancer.Start.Commands;

public sealed class StartAiInterviewCommandValidator : AbstractValidator<StartAiInterviewCommand>
{
    public StartAiInterviewCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.JobPostId).NotEmpty();
        RuleFor(x => x.Mode).Must(x => new[] { "text", "voice" }.Contains(x.ToLowerInvariant()));
        RuleFor(x => x.Language).Must(x => new[] { "auto", "en", "vi" }.Contains(x.ToLowerInvariant()));
    }
}
