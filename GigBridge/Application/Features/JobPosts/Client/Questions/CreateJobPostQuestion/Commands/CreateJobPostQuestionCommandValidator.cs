using FluentValidation;

namespace Application.Features.JobPosts.Client.Questions.CreateJobPostQuestion.Commands;

public class CreateJobPostQuestionCommandValidator : AbstractValidator<CreateJobPostQuestionCommand>
{
    public CreateJobPostQuestionCommandValidator()
    {
        RuleFor(x => x.JobPostsId)
            .NotEmpty().WithMessage("JobPostsId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request body is required.");

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.QuestionText)
                .NotEmpty().WithMessage("QuestionText is required.")
                .MaximumLength(1000).WithMessage("QuestionText must not exceed 1000 characters.");

            RuleFor(x => x.Request.OrderIndex)
                .GreaterThanOrEqualTo(0).WithMessage("OrderIndex must be greater than or equal to 0.");
        });
    }
}
