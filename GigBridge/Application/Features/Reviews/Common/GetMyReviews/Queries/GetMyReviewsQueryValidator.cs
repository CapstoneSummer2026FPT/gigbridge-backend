using FluentValidation;

namespace Application.Features.Reviews.Common.GetMyReviews.Queries;

public sealed class GetMyReviewsQueryValidator : AbstractValidator<GetMyReviewsQuery>
{
    public GetMyReviewsQueryValidator()
    {
        RuleFor(query => query.UserId).NotEmpty();
        RuleFor(query => query.Direction)
            .Must(direction => direction.Equals("received", StringComparison.OrdinalIgnoreCase) ||
                               direction.Equals("sent", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Direction must be received or sent.");
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 50);
    }
}
