using FluentValidation;

namespace Application.Features.JobPosts.Public.GetJobPostDetail.Queries;

public class GetJobPostDetailQueryValidator : AbstractValidator<GetJobPostDetailQuery>
{
    public GetJobPostDetailQueryValidator()
    {
        RuleFor(x => x.JobPostsId)
            .NotEmpty()
            .WithMessage("JobPostsId is required.");
    }
}
