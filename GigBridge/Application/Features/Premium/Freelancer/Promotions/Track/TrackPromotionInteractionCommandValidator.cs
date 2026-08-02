using Application.Features.Premium.Freelancer.Promotions.Common;
using FluentValidation;

namespace Application.Features.Premium.Freelancer.Promotions.Track;

public sealed class TrackPromotionInteractionCommandValidator : AbstractValidator<TrackPromotionInteractionCommand>
{
    public TrackPromotionInteractionCommandValidator()
    {
        RuleFor(x => x.PromotionId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.VisitorKey)
            .NotEmpty()
            .MaximumLength(PromotionPolicy.Defaults.VisitorKeyMaxLength);
    }
}
