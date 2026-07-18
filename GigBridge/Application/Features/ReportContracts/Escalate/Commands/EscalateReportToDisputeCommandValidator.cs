using FluentValidation;

namespace Application.Features.ReportContracts.Escalate.Commands;

public sealed class EscalateReportToDisputeCommandValidator : AbstractValidator<EscalateReportToDisputeCommand>
{
    private const int ReasonMaxLength = 2000;

    public EscalateReportToDisputeCommandValidator()
    {
        RuleFor(c => c.ContractId)
            .NotEmpty();

        RuleFor(c => c.ReportId)
            .NotEmpty();

        RuleFor(c => c.Reason)
            .NotEmpty()
            .WithMessage("Dispute reason is required.")
            .Must(r => !string.IsNullOrWhiteSpace(r))
            .WithMessage("Dispute reason cannot be only whitespace.")
            .MaximumLength(ReasonMaxLength)
            .WithMessage($"Dispute reason must not exceed {ReasonMaxLength} characters.");

        RuleFor(c => c.Title)
            .MaximumLength(200)
            .When(c => c.Title is not null);

        RuleFor(c => c.Description)
            .MaximumLength(5000)
            .When(c => c.Description is not null);

        RuleFor(c => c.RequestedResolution)
            .MaximumLength(2000)
            .When(c => c.RequestedResolution is not null);

        RuleFor(c => c.ClaimedAmount)
            .GreaterThan(0)
            .When(c => c.ClaimedAmount.HasValue)
            .WithMessage("Claimed amount must be greater than 0.");
    }
}
