using Application.Common.Interfaces.IService;
using FluentValidation;

namespace Application.Features.JobPosts.Client.SaveDraftJobPost.Commands;

public sealed class SaveDraftJobPostCommandValidator
    : AbstractValidator<SaveDraftJobPostCommand>
{
    private readonly IContentModerationService _contentModerationService;

    public SaveDraftJobPostCommandValidator(IContentModerationService contentModerationService)
    {
        _contentModerationService = contentModerationService;

        RuleFor(x => x.JobPostId)
            .NotEmpty().WithMessage("JobPostId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request body is required.");

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.Title)
                .MaximumLength(200)
                .When(x => !string.IsNullOrWhiteSpace(x.Request.Title))
                .WithMessage("Title must not exceed 200 characters.");

            RuleFor(x => x.Request)
                .Custom((request, context) =>
                {
                    if (request is null)
                    {
                        return;
                    }

                    var moderationResult = _contentModerationService.ValidateJobPostContent(
                        request.Title,
                        request.Description);

                    if (moderationResult.IsAllowed)
                    {
                        return;
                    }

                    foreach (var violation in GetViolationMessages(moderationResult))
                    {
                        context.AddFailure("JobPostContent", violation);
                    }
                });

            RuleFor(x => x.Request.BudgetMin)
                .GreaterThanOrEqualTo(0)
                .When(x => x.Request.BudgetMin.HasValue)
                .WithMessage("BudgetMin must be greater than or equal to 0.");

            RuleFor(x => x.Request.BudgetMax)
                .GreaterThanOrEqualTo(0)
                .When(x => x.Request.BudgetMax.HasValue)
                .WithMessage("BudgetMax must be greater than or equal to 0.");

            RuleFor(x => x.Request)
                .Must(request =>
                    !request.BudgetMin.HasValue ||
                    !request.BudgetMax.HasValue ||
                    request.BudgetMin.Value <= request.BudgetMax.Value)
                .WithMessage("BudgetMin must be less than or equal to BudgetMax.");

            RuleFor(x => x.Request.Visibility)
                .Must(visibility =>
                    !visibility.HasValue ||
                    visibility.Value == 0 ||
                    visibility.Value == 1 ||
                    visibility.Value == 2)
                .WithMessage("Visibility must be 0=Public, 1=Private, or 2=InviteOnly.");

            RuleFor(x => x.Request.EndDate)
                .GreaterThan(DateTime.UtcNow)
                .When(x => x.Request.EndDate.HasValue)
                .WithMessage("EndDate must be in the future.");

            RuleForEach(x => x.Request.Questions)
                .ChildRules(question =>
                {
                    question.RuleFor(x => x.QuestionText)
                        .MaximumLength(1000)
                        .When(x => !string.IsNullOrWhiteSpace(x.QuestionText))
                        .WithMessage("QuestionText must not exceed 1000 characters.");

                    question.RuleFor(x => x.OrderIndex)
                        .GreaterThanOrEqualTo(0)
                        .WithMessage("OrderIndex must be greater than or equal to 0.");
                });

            RuleForEach(x => x.Request.MilestonePlans)
                .ChildRules(milestone =>
                {
                    milestone.RuleFor(x => x.OrderIndex).GreaterThanOrEqualTo(0);
                    milestone.RuleFor(x => x.Title).MaximumLength(200);
                    milestone.RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
                    milestone.RuleForEach(x => x.WorkItems).ChildRules(item =>
                    {
                        item.RuleFor(x => x.OrderIndex).GreaterThanOrEqualTo(0);
                        item.RuleFor(x => x.Title).MaximumLength(200);
                    });
                });

            RuleFor(x => (x.Request.SkillIds != null ? x.Request.SkillIds.Count : 0) + 
                         (x.Request.CustomSkillNames != null ? x.Request.CustomSkillNames.Count : 0))
                .LessThanOrEqualTo(10)
                .WithMessage("You can select up to 10 skills in total (including custom skills).");
        });
    }

    private static IEnumerable<string> GetViolationMessages(ContentModerationResult moderationResult)
    {
        var violations = moderationResult.Violations
            .Where(violation => !string.IsNullOrWhiteSpace(violation))
            .Distinct()
            .ToArray();

        return violations.Length > 0
            ? violations
            : new[] { ContentModerationMessages.JobPostContentViolation };
    }
}
