using FluentValidation;

namespace Application.Features.Proposals.Common.UpdateProposalStatus.Commands;

public class UpdateProposalStatusCommandValidator
    : AbstractValidator<UpdateProposalStatusCommand>
{
    public UpdateProposalStatusCommandValidator()
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

        RuleFor(x => x.Request.Status)
            .Must(status => status == 1 || status == 2 || status == 3 || status == 4)
            .WithMessage("Status must be 1=Shortlisted, 2=Accepted, 3=Rejected, or 4=Withdrawn.");
    }
}