using FluentValidation;

namespace Application.Features.JobPosts.Client.Questions.CreateBulkJobPostQuestions.Commands;

public class CreateBulkJobPostQuestionsCommandValidator
    : AbstractValidator<CreateBulkJobPostQuestionsCommand>
{
    public CreateBulkJobPostQuestionsCommandValidator()
    {
        RuleFor(x => x.JobPostsId)
            .NotEmpty().WithMessage("JobPostsId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request body is required.");

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.Questions)
                .NotNull().WithMessage("Questions is required.")
                .NotEmpty().WithMessage("Questions must not be empty.")
                .Must(questions => questions is null || questions.Select(question => question.OrderIndex).Distinct().Count() == questions.Count)
                .WithMessage("Questions must not contain duplicate OrderIndex values.");

            RuleForEach(x => x.Request.Questions)
                .ChildRules(question =>
                {
                    question.RuleFor(x => x.QuestionText)
                        .NotEmpty().WithMessage("QuestionText is required.")
                        .MaximumLength(1000).WithMessage("QuestionText must not exceed 1000 characters.");

                    question.RuleFor(x => x.OrderIndex)
                        .GreaterThanOrEqualTo(0).WithMessage("OrderIndex must be greater than or equal to 0.");
                });
        });
    }
}
