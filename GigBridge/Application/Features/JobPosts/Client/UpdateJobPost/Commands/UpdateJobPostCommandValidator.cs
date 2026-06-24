using FluentValidation;

namespace Application.Features.JobPosts.Client.UpdateJobPost.Commands;

public class UpdateJobPostCommandValidator : AbstractValidator<UpdateJobPostCommand>
{
    public UpdateJobPostCommandValidator()
    {
        RuleFor(x => x.JobPostId)
            .NotEmpty()
            .WithMessage("JobPostId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage("Request body is required.");

        RuleFor(x => x.Request.Title)
            .NotEmpty()
            .WithMessage("Title is required.")
            .MaximumLength(200)
            .WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Request.Description)
            .NotEmpty()
            .WithMessage("Description is required.");

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
            .NotNull()
            .WithMessage("Visibility is required.")
            .Must(visibility => visibility == 0 || visibility == 1 || visibility == 2)
            .WithMessage("Visibility must be 0=Public, 1=Private, or 2=InviteOnly.");

        RuleFor(x => x.Request.EndDate)
            .GreaterThan(DateTime.UtcNow)
            .When(x => x.Request.EndDate.HasValue)
            .WithMessage("EndDate must be in the future.");

        RuleFor(x => (x.Request.SkillIds != null ? x.Request.SkillIds.Count : 0) + 
                     (x.Request.CustomSkillNames != null ? x.Request.CustomSkillNames.Count : 0))
            .LessThanOrEqualTo(10)
            .When(x => x.Request != null)
            .WithMessage("You can select up to 10 skills in total (including custom skills).");
    }
}
