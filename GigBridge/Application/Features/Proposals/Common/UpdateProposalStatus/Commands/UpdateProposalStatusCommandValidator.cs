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
            .Must(status => status >= 1 && status <= 5)
            .WithMessage("Status must be 1=Pending, 2=Shortlisted, 3=Accepted, 4=Rejected, or 5=Withdrawn.")
            .When(x => x.Request is not null);
    }
}