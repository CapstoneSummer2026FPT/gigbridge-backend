using FluentValidation;

namespace Application.Features.Admin.Cheating.ReviewViolation.Commands;

public class ReviewCheatingViolationCommandValidator : AbstractValidator<ReviewCheatingViolationCommand>
{
    public ReviewCheatingViolationCommandValidator()
    {
        RuleFor(command => command.ViolationId).NotEmpty();
        RuleFor(command => command.AdminUserId).NotEmpty();
        RuleFor(command => command.Request).NotNull();
        RuleFor(command => command.Request.AdminNote)
            .MaximumLength(1000)
            .When(command => command.Request is not null);
    }
}
