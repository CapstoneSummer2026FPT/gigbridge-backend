using FluentValidation;

namespace Application.Features.Subscriptions.Freelancer.Cancel;

public sealed class CancelSubscriptionCommandValidator : AbstractValidator<CancelSubscriptionCommand>
{
    public CancelSubscriptionCommandValidator() => RuleFor(command => command.UserId).NotEmpty();
}
