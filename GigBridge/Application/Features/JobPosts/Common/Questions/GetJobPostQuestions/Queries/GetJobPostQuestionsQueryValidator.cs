using FluentValidation;

namespace Application.Features.JobPosts.Common.Questions.GetJobPostQuestions.Queries;

public class GetJobPostQuestionsQueryValidator : AbstractValidator<GetJobPostQuestionsQuery>
{
    public GetJobPostQuestionsQueryValidator()
    {
        RuleFor(x => x.JobPostsId)
            .NotEmpty().WithMessage("JobPostsId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.");
    }
}
