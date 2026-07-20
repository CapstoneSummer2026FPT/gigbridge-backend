using FluentValidation;

namespace Application.Features.ReportContracts.Respond.Commands;

public sealed class RespondToReportCommandValidator : AbstractValidator<RespondToReportCommand>
{
    public RespondToReportCommandValidator()
    {
        RuleFor(v => v.ResolutionAction)
            .InclusiveBetween(0, 3)
            .WithMessage("Invalid resolution action.");

        RuleFor(v => v.Explanation)
            .MaximumLength(5000)
            .WithMessage("Explanation must not exceed 5000 characters.");

        RuleFor(v => v.ProposedResolution)
            .MaximumLength(5000)
            .WithMessage("Proposed resolution must not exceed 5000 characters.");

        RuleFor(v => v.RejectReason)
            .MaximumLength(5000)
            .WithMessage("Reject reason must not exceed 5000 characters.");
    }
}
