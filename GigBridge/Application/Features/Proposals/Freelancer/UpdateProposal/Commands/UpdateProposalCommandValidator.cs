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

        RuleFor(x => x.Request.ProposedRate)
            .GreaterThan(0)
            .When(x => x.Request.ProposedRate.HasValue)
            .WithMessage("ProposedRate must be greater than 0.");

        RuleFor(x => x.Request.ProposedDuration)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Request.ProposedDuration))
            .WithMessage("ProposedDuration must not exceed 100 characters.");
    }
}