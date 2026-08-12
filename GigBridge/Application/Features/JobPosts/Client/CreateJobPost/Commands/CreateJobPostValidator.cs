using Application.Features.JobPosts.Common.ContentModeration;
using FluentValidation;

namespace Application.Features.JobPosts.Client.CreateJobPost.Commands;

public class CreateJobPostValidator : AbstractValidator<CreateJobPostCommand>
{
    private readonly IContentModerationService _contentModerationService;

    public CreateJobPostValidator(IContentModerationService contentModerationService)
    {
        _contentModerationService = contentModerationService;

        RuleFor(x => x.Request.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Request.Description)
            .NotEmpty().WithMessage("Description is required.");

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

        RuleFor(x => x.Request.BudgetMax)
            .GreaterThanOrEqualTo(x => x.Request.BudgetMin)
            .When(x => x.Request.BudgetMin.HasValue && x.Request.BudgetMax.HasValue)
            .WithMessage("BudgetMax must be greater than or equal to BudgetMin.");

        RuleFor(x => x.Request.EndDate)
            .GreaterThan(DateTime.UtcNow)
            .When(x => x.Request.EndDate.HasValue)
            .WithMessage("EndDate must be in the future.");

        RuleFor(x => (x.Request.SkillIds != null ? x.Request.SkillIds.Count : 0) + 
                     (x.Request.CustomSkillNames != null ? x.Request.CustomSkillNames.Count : 0))
            .LessThanOrEqualTo(10)
            .WithMessage("You can select up to 10 skills in total (including custom skills).");
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
