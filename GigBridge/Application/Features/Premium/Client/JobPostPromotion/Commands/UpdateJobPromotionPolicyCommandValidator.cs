using FluentValidation;

namespace Application.Features.Premium.Client.JobPostPromotion.Commands;

public sealed class UpdateJobPromotionPolicyCommandValidator : AbstractValidator<UpdateJobPromotionPolicyCommand>
{
    public UpdateJobPromotionPolicyCommandValidator()
    {
        RuleFor(x => x.AdminUserId).NotEmpty();
        RuleFor(x => x.Request.TokenCost).GreaterThan(0).Must(x => x == decimal.Truncate(x));
        RuleFor(x => x.Request.DurationDays).InclusiveBetween(1, 365);
    }
}
