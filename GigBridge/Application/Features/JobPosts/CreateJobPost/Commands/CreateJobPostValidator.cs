using FluentValidation;
using System;

namespace Application.Features.JobPosts.CreateJobPost.Commands;

public class CreateJobPostValidator : AbstractValidator<CreateJobPostCommand>
{
    public CreateJobPostValidator()
    {
        RuleFor(x => x.Request.Title)
            .NotEmpty().WithMessage("Tiêu đề không được để trống.")
            .MaximumLength(200).WithMessage("Tiêu đề không vượt quá 200 ký tự.");

        RuleFor(x => x.Request.Description)
            .NotEmpty().WithMessage("Mô tả công việc không được để trống.");

        RuleFor(x => x.Request.BudgetType)
            .InclusiveBetween(0, 1).WithMessage("BudgetType không hợp lệ (0 hoặc 1).");

        RuleFor(x => x.Request.BudgetMax)
            .GreaterThan(x => x.Request.BudgetMin)
            .When(x => x.Request.BudgetMin.HasValue && x.Request.BudgetMax.HasValue)
            .WithMessage("Ngân sách tối đa phải lớn hơn ngân sách tối thiểu.");

        RuleFor(x => x.Request.ApplicationDeadline)
            .GreaterThan(DateTime.UtcNow).When(x => x.Request.ApplicationDeadline.HasValue)
            .WithMessage("Hạn chót ứng tuyển phải là ngày trong tương lai.");
    }
}