using FluentValidation;

namespace Application.Features.Premium.Client.JobPostPromotion.Commands;

public sealed class PromoteJobPostCommandValidator : AbstractValidator<PromoteJobPostCommand>
{
    public PromoteJobPostCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.JobPostId).NotEmpty();
        RuleFor(x => x.Request.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}
