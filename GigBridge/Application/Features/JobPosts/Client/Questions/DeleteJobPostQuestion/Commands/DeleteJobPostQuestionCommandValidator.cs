using FluentValidation;

namespace Application.Features.JobPosts.Client.Questions.DeleteJobPostQuestion.Commands;

public class DeleteJobPostQuestionCommandValidator
    : AbstractValidator<DeleteJobPostQuestionCommand>
{
    public DeleteJobPostQuestionCommandValidator()
    {
        RuleFor(x => x.JobPostsId)
            .NotEmpty().WithMessage("JobPostsId is required.");

        RuleFor(x => x.JobPostQuestionsId)
            .NotEmpty().WithMessage("JobPostQuestionsId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");
    }
}
