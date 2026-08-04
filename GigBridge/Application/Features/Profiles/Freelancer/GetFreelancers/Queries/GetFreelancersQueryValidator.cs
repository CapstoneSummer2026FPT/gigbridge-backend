using FluentValidation;

namespace Application.Features.Profiles.FreelancerProfile.GetFreelancers.Queries;

public sealed class GetFreelancersQueryValidator : AbstractValidator<GetFreelancersQuery>
{
    private static readonly string[] SupportedSorts = ["featured", "rating", "elo", "newest"];
    private static readonly string[] SupportedAvailability =
        ["available", "busy", "fulltime", "parttime", "notavailable", "0", "1", "2"];

    public GetFreelancersQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThan(0);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 50);

        RuleFor(query => query.Search)
            .MaximumLength(200);

        RuleFor(query => query.MinRating)
            .InclusiveBetween(0, 5)
            .When(query => query.MinRating.HasValue);

        RuleFor(query => query.Sort)
            .Must(value => value is null || SupportedSorts.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Sort must be one of: featured, rating, elo, newest.");

        RuleFor(query => query.AvailabilityStatus)
            .Must(value =>
                value is null ||
                SupportedAvailability.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage(
                "AvailabilityStatus must be one of: available, busy, fulltime, parttime, notavailable, 0, 1, 2.");

        RuleFor(query => query.Skills)
            .Must(skills => skills is null || skills.Count <= 20)
            .WithMessage("At most 20 skills can be requested.");

        RuleForEach(query => query.Skills)
            .NotEmpty()
            .MaximumLength(100);
    }
}
