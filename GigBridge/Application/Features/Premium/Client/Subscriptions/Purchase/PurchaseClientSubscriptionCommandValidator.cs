using FluentValidation;

namespace Application.Features.Premium.Client.Subscriptions.Purchase;

public sealed class PurchaseClientSubscriptionCommandValidator : AbstractValidator<PurchaseClientSubscriptionCommand>
{
    public PurchaseClientSubscriptionCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.Request.PlanId).NotEmpty();
        RuleFor(command => command.Request.IdempotencyKey).NotEmpty().MaximumLength(100);
    }
}
