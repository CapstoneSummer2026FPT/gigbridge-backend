using FluentValidation;
using System;

namespace Application.Features.JobPosts.Client.CreateJobPost.Commands;

public class CreateJobPostValidator : AbstractValidator<CreateJobPostCommand>
{
    public CreateJobPostValidator()
    {
        RuleFor(x => x.Request.Title)
            .NotEmpty().WithMessage("Tiêu d? không du?c d? tr?ng.")
            .MaximumLength(200).WithMessage("Tiêu d? không vu?t quá 200 ký t?.");

        RuleFor(x => x.Request.Description)
            .NotEmpty().WithMessage("Mô t? công vi?c không du?c d? tr?ng.");

        RuleFor(x => x.Request.BudgetType)
            .InclusiveBetween(0, 1).WithMessage("BudgetType không h?p l? (0 ho?c 1).");

        RuleFor(x => x.Request.BudgetMax)
            .GreaterThan(x => x.Request.BudgetMin)
            .When(x => x.Request.BudgetMin.HasValue && x.Request.BudgetMax.HasValue)
            .WithMessage("Ngân sách t?i da ph?i l?n hon ngân sách t?i thi?u.");

        RuleFor(x => x.Request.EndDate)
            .GreaterThan(DateTime.UtcNow).When(x => x.Request.EndDate.HasValue)
            .WithMessage("H?n chót ?ng tuy?n ph?i là ngày trong tuong lai.");
    }
}