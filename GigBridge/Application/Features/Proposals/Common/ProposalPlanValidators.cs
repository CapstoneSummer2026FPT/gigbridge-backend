using Application.Features.Proposals.Common.DTOs;
using FluentValidation;

namespace Application.Features.Proposals.Common;

internal sealed class ProposalWorkBreakdownItemValidator
    : AbstractValidator<ProposalWorkBreakdownItemDto>
{
    public ProposalWorkBreakdownItemValidator()
    {
        RuleFor(item => item.Title)
            .MaximumLength(200)
            .WithMessage("Work item title must not exceed 200 characters.");

        RuleFor(item => item.EstimatedDuration)
            .MaximumLength(100)
            .WithMessage("Work item estimated duration must not exceed 100 characters.");
    }
}

internal sealed class ProposalMilestonePlanValidator
    : AbstractValidator<ProposalMilestonePlanDto>
{
    public ProposalMilestonePlanValidator()
    {
        RuleFor(item => item.Title)
            .MaximumLength(200)
            .WithMessage("Milestone title must not exceed 200 characters.");

        RuleFor(item => item.EstimatedDuration)
            .MaximumLength(100)
            .WithMessage("Milestone estimated duration must not exceed 100 characters.");

        RuleForEach(item => item.WorkItems)
            .SetValidator(new ProposalWorkBreakdownItemValidator());
    }
}
