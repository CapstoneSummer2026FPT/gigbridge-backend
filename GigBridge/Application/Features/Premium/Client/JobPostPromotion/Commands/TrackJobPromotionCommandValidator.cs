using FluentValidation;

namespace Application.Features.Premium.Client.JobPostPromotion.Commands;

public sealed class TrackJobPromotionCommandValidator : AbstractValidator<TrackJobPromotionCommand>
{
    public TrackJobPromotionCommandValidator()
    {
        RuleFor(x => x.PromotionId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.VisitorKey).NotEmpty().MaximumLength(128);
    }
}
