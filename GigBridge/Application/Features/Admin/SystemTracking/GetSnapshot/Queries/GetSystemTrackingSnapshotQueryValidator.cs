using FluentValidation;

namespace Application.Features.Admin.SystemTracking.GetSnapshot.Queries;

public sealed class GetSystemTrackingSnapshotQueryValidator : AbstractValidator<GetSystemTrackingSnapshotQuery>
{
    public GetSystemTrackingSnapshotQueryValidator()
    {
        RuleFor(x => x.Environment).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Limit).InclusiveBetween(1, 200);
    }
}
