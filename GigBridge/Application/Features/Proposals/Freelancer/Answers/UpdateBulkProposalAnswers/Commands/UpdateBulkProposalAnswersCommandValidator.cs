using FluentValidation;

namespace Application.Features.Proposals.Freelancer.Answers.UpdateBulkProposalAnswers.Commands;

public class UpdateBulkProposalAnswersCommandValidator
    : AbstractValidator<UpdateBulkProposalAnswersCommand>
{
    public UpdateBulkProposalAnswersCommandValidator()
    {
        RuleFor(x => x.ProposalsId)
            .NotEmpty().WithMessage("ProposalsId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request body is required.");

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.Answers)
                .NotNull().WithMessage("Answers is required.")
                .NotEmpty().WithMessage("Answers must not be empty.")
                .Must(answers => answers is null || answers.Select(answer => answer.JobPostQuestionId).Distinct().Count() == answers.Count)
                .WithMessage("Answers must not contain duplicate JobPostQuestionId values.");

            RuleForEach(x => x.Request.Answers)
                .ChildRules(answer =>
                {
                    answer.RuleFor(x => x.JobPostQuestionId)
                        .NotEmpty().WithMessage("JobPostQuestionId is required.");

                    answer.RuleFor(x => x.AnswerText)
                        .MaximumLength(4000).WithMessage("AnswerText must not exceed 4000 characters.");
                });
        });
    }
}
