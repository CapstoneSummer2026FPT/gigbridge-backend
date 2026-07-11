using FluentValidation;

namespace Application.Features.Subscriptions.Freelancer.Purchase;

public sealed class PurchaseSubscriptionCommandValidator : AbstractValidator<PurchaseSubscriptionCommand>
{
    public PurchaseSubscriptionCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.Request).NotNull();
        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.PlanId).NotEmpty();
            RuleFor(command => command.Request.IdempotencyKey).NotEmpty();
        });
    }
}
