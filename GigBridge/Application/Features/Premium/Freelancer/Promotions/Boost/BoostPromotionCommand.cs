using Application.Features.Premium.Freelancer.Promotions.DTOs;
using FluentValidation;
using MediatR;
namespace Application.Features.Premium.Freelancer.Promotions.Boost;
public sealed record BoostPromotionRequest(decimal TokenAmount, string IdempotencyKey);
public sealed record BoostPromotionCommand(Guid UserId, Guid PromotionId, BoostPromotionRequest Request) : IRequest<PromotionDto>;
public sealed class BoostPromotionCommandValidator : AbstractValidator<BoostPromotionCommand>
{
    public BoostPromotionCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty(); RuleFor(x => x.PromotionId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        When(x => x.Request is not null, () => { RuleFor(x => x.Request.TokenAmount).GreaterThan(0); RuleFor(x => x.Request.IdempotencyKey).NotEmpty(); });
    }
}
