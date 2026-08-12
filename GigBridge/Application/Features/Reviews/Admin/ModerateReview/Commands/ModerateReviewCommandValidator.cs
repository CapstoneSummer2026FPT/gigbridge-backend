using Domain.Enums.Reviews;
using FluentValidation;

namespace Application.Features.Reviews.Admin.ModerateReview.Commands;

public sealed class ModerateReviewCommandValidator : AbstractValidator<ModerateReviewCommand>
{
    public ModerateReviewCommandValidator()
    {
        RuleFor(command => command.ReviewId).NotEmpty();
        RuleFor(command => command.AdminId).NotEmpty();
        RuleFor(command => command.Request).NotNull();
        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.Status)
                .IsInEnum()
                .Must(status => status is ReviewModerationStatus.Active or ReviewModerationStatus.Hidden);
            RuleFor(command => command.Request.Note).NotEmpty().MinimumLength(10).MaximumLength(1000);
        });
    }
}
