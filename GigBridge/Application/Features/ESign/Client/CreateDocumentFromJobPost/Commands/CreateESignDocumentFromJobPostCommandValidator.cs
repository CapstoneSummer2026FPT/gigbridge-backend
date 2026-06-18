using FluentValidation;

namespace Application.Features.ESign.Client.CreateDocumentFromJobPost.Commands;

public sealed class CreateESignDocumentFromJobPostCommandValidator
    : AbstractValidator<CreateESignDocumentFromJobPostCommand>
{
    public CreateESignDocumentFromJobPostCommandValidator()
    {
        RuleFor(command => command.JobPostId)
            .NotEmpty()
            .WithMessage("JobPostId is required.");

        RuleFor(command => command.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");
    }
}
