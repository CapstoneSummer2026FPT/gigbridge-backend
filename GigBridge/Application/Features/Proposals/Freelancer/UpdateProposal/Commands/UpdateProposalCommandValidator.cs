using FluentValidation;

namespace Application.Features.Proposals.Freelancer.UpdateProposal.Commands;

public class UpdateProposalCommandValidator : AbstractValidator<UpdateProposalCommand>
{
    public UpdateProposalCommandValidator()
    {
        RuleFor(x => x.ProposalId)
            .NotEmpty()
            .WithMessage("ProposalId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage("Request body is required.");

        RuleFor(x => x.Request.CoverLetter)
            .MaximumLength(4000)
            .When(x => !string.IsNullOrWhiteSpace(x.Request.CoverLetter))
            .WithMessage("CoverLetter must not exceed 4000 characters.");

        RuleFor(x => x.Request.ProposedBudget)
            .Must(value => value.HasValue && global::Application.Features.Proposals.Common.ProposalTotalsCalculator.IsValidAmount(value.Value))
            .WithMessage("ProposedBudget must be greater than 0, use at most 2 decimal places, and fit decimal(18,2).")
            .When(x => x.Request.ProposedBudget.HasValue);

        RuleFor(x => x.Request.ProposedDuration)
            .MaximumLength(100)
            .Must(value => global::Application.Features.Proposals.Common.ProposalTotalsCalculator.IsValidDuration(value))
            .When(x => !string.IsNullOrWhiteSpace(x.Request.ProposedDuration))
            .WithMessage("ProposedDuration must be a positive whole number in days, weeks, or months.");

        RuleForEach(x => x.Request.WorkBreakdownItems)
            .SetValidator(new global::Application.Features.Proposals.Common.ProposalWorkBreakdownItemValidator());

        RuleForEach(x => x.Request.MilestonePlans)
            .SetValidator(new global::Application.Features.Proposals.Common.ProposalMilestonePlanValidator());
    }
}
