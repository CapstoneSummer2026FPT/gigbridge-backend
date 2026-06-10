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

        RuleFor(x => x.Request.BudgetType)
            .Must(x => x == 0 || x == 1)
            .WithMessage("BudgetType must be 0=Fixed or 1=Hourly.");

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

        RuleFor(x => x.Request.MaxHires)
            .GreaterThan(0)
            .When(x => x.Request.MaxHires.HasValue)
            .WithMessage("MaxHires must be greater than 0.");

        RuleFor(x => x.Request.ExperienceLevelRequired)
            .Must(x => x == null || x == 0 || x == 1 || x == 2)
            .WithMessage("ExperienceLevelRequired must be 0=Entry, 1=Intermediate, or 2=Expert.");

        RuleFor(x => x.Request.LocationType)
            .Must(x => x == null || x == 0 || x == 1 || x == 2)
            .WithMessage("LocationType must be 0=Remote, 1=OnSite, or 2=Hybrid.");

        RuleFor(x => x.Request.EndDate)
            .GreaterThan(DateTime.UtcNow)
            .When(x => x.Request.EndDate.HasValue)
            .WithMessage("EndDate must be in the future.");
    }
}