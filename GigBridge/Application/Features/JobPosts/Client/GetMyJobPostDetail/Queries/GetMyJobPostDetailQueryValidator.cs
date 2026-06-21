using FluentValidation;

namespace Application.Features.JobPosts.Client.GetMyJobPostDetail.Queries;

public class GetMyJobPostDetailQueryValidator : AbstractValidator<GetMyJobPostDetailQuery>
{
    public GetMyJobPostDetailQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.JobPostId)
            .NotEmpty()
            .WithMessage("JobPostId is required.");
    }
}
