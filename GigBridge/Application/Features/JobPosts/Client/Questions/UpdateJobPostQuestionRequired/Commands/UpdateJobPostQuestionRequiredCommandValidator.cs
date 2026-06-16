using FluentValidation;

namespace Application.Features.JobPosts.Client.Questions.UpdateJobPostQuestionRequired.Commands;

public class UpdateJobPostQuestionRequiredCommandValidator
    : AbstractValidator<UpdateJobPostQuestionRequiredCommand>
{
    public UpdateJobPostQuestionRequiredCommandValidator()
    {
        RuleFor(x => x.JobPostsId)
            .NotEmpty().WithMessage("JobPostsId is required.");

        RuleFor(x => x.JobPostQuestionsId)
            .NotEmpty().WithMessage("JobPostQuestionsId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request body is required.");
    }
}
