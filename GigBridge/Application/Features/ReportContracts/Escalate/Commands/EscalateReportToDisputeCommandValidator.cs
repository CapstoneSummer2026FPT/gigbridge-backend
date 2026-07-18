using FluentValidation;

namespace Application.Features.ReportContracts.Escalate.Commands;

public sealed class EscalateReportToDisputeCommandValidator : AbstractValidator<EscalateReportToDisputeCommand>
{
    public EscalateReportToDisputeCommandValidator()
    {
        RuleFor(c => c.ContractId)
            .NotEmpty();

        RuleFor(c => c.ReportId)
            .NotEmpty();

        RuleFor(c => c.Title)
            .NotEmpty()
            .WithMessage("Dispute title is required.")
            .MaximumLength(200);

        RuleFor(c => c.Description)
            .NotEmpty()
            .WithMessage("Dispute description is required.")
            .MaximumLength(5000);

        RuleFor(c => c.RequestedResolution)
            .NotEmpty()
            .WithMessage("Requested resolution is required.")
            .MaximumLength(2000);

        RuleFor(c => c.ClaimedAmount)
            .NotNull()
            .WithMessage("Claimed amount is required.")
            .GreaterThanOrEqualTo(0)
            .WithMessage("Claimed amount cannot be negative.");

        RuleFor(c => c.Urgency)
            .NotNull()
            .WithMessage("Dispute urgency is required.")
            .IsInEnum()
            .WithMessage("Dispute urgency is invalid.");

        RuleFor(c => c.DeclarationAccepted)
            .Equal(true)
            .WithMessage("You must accept the dispute declaration before submitting.");

        RuleFor(c => c.EvidenceFiles)
            .Must(files => files.Count <= 5)
            .WithMessage("No more than 5 additional evidence files can be uploaded at a time.");
    }
}
