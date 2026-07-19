using FluentValidation;

namespace Application.Features.Premium.Client.Subscriptions.Cancel;

public sealed class CancelClientSubscriptionCommandValidator
    : AbstractValidator<CancelClientSubscriptionCommand>
{
    public CancelClientSubscriptionCommandValidator() =>
        RuleFor(command => command.UserId).NotEmpty();
}
