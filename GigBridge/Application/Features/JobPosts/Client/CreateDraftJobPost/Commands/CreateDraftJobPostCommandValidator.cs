using FluentValidation;

namespace Application.Features.JobPosts.Client.CreateDraftJobPost.Commands;

public class CreateDraftJobPostCommandValidator : AbstractValidator<CreateDraftJobPostCommand>
{
    public CreateDraftJobPostCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");
    }
}
