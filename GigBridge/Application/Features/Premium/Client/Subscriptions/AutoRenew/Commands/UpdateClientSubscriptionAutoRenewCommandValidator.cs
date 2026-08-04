using FluentValidation;

namespace Application.Features.Premium.Client.Subscriptions.AutoRenew.Commands;

public sealed class UpdateClientSubscriptionAutoRenewCommandValidator
    : AbstractValidator<UpdateClientSubscriptionAutoRenewCommand>
{
    public UpdateClientSubscriptionAutoRenewCommandValidator() =>
        RuleFor(command => command.UserId).NotEmpty();
}
