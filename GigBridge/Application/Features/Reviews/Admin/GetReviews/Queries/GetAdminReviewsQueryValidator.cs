using FluentValidation;

namespace Application.Features.Reviews.Admin.GetReviews.Queries;

public sealed class GetAdminReviewsQueryValidator : AbstractValidator<GetAdminReviewsQuery>
{
    public GetAdminReviewsQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Rating).InclusiveBetween(1, 5).When(query => query.Rating.HasValue);
        RuleFor(query => query.ReviewerRole).Must(role => role is 0 or 1).When(query => query.ReviewerRole.HasValue);
        RuleFor(query => query.RevieweeRole).Must(role => role is 0 or 1).When(query => query.RevieweeRole.HasValue);
        RuleFor(query => query.ModerationStatus).IsInEnum().When(query => query.ModerationStatus.HasValue);
        RuleFor(query => query.Search).MaximumLength(200);
    }
}
