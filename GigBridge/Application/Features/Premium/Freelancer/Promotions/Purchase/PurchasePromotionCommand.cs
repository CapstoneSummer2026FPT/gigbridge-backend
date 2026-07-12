using Application.Features.Premium.Freelancer.Promotions.DTOs;
using FluentValidation;
using MediatR;

namespace Application.Features.Premium.Freelancer.Promotions.Purchase;

public sealed record PurchasePromotionRequest(
    decimal TokenAmount, string IdempotencyKey, string PhotoUrl, string DisplayName,
    string? Quote, bool ShowQuote, string? JobTitle, bool ShowJobTitle);

public sealed record PurchasePromotionCommand(Guid UserId, PurchasePromotionRequest Request)
    : IRequest<PromotionDto>;

public sealed class PurchasePromotionCommandValidator : AbstractValidator<PurchasePromotionCommand>
{
    public PurchasePromotionCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.Request).NotNull();
        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.IdempotencyKey).NotEmpty();
            RuleFor(command => command.Request.TokenAmount).GreaterThanOrEqualTo(0);
            RuleFor(command => command.Request.PhotoUrl).NotEmpty();
            RuleFor(command => command.Request.DisplayName).NotEmpty();
        });
    }
}
