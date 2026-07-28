using Application.Features.Premium.Freelancer.Promotions.DTOs;
using FluentValidation;
using MediatR;

namespace Application.Features.Premium.Freelancer.Promotions.End;

public sealed record EndPromotionCommand(Guid UserId, Guid PromotionId) : IRequest<PromotionDto>;

public sealed class EndPromotionCommandValidator : AbstractValidator<EndPromotionCommand>
{
    public EndPromotionCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.PromotionId).NotEmpty();
    }
}
