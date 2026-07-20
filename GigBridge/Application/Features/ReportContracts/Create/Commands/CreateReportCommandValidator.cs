using FluentValidation;

namespace Application.Features.ReportContracts.Create.Commands;

public sealed class CreateReportCommandValidator : AbstractValidator<CreateReportCommand>
{
    public CreateReportCommandValidator()
    {
        RuleFor(v => v.IssueType)
            .InclusiveBetween(0, 6)
            .WithMessage("Invalid issue type.");

        RuleFor(v => v.Description)
            .NotEmpty()
            .MaximumLength(5000)
            .WithMessage("Description must not exceed 5000 characters.");

        RuleFor(v => v.DesiredResolution)
            .NotEmpty()
            .MaximumLength(5000)
            .WithMessage("Desired resolution must not exceed 5000 characters.");
    }
}
