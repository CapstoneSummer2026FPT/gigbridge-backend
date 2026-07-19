using FluentValidation;

namespace Application.Features.Premium.Client.AiInterviews.Create.Commands;

public sealed class CreateAiInterviewCommandValidator : AbstractValidator<CreateAiInterviewCommand>
{
    public CreateAiInterviewCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.JobPostId).NotEmpty();
        RuleFor(x => x.Request.Language).Must(x => new[] { "auto", "en", "vi" }.Contains(x.ToLowerInvariant()));
        RuleFor(x => x.Request.Mode).Must(x => new[] { "text", "voice" }.Contains(x.ToLowerInvariant()));
        RuleFor(x => x.Request.QuestionCount).InclusiveBetween(1, 20);
    }
}
