using FluentValidation;

namespace Application.Features.Disputes.Evidence.Add.Commands;

public sealed class AddDisputeEvidenceCommandValidator : AbstractValidator<AddDisputeEvidenceCommand>
{
    public AddDisputeEvidenceCommandValidator()
    {
        RuleFor(command => command.ContractId).NotEmpty();
        RuleFor(command => command.DisputeId).NotEmpty();
        RuleFor(command => command.Files)
            .NotEmpty()
            .WithMessage("At least one evidence file is required.")
            .Must(files => files.Count <= 5)
            .WithMessage("No more than 5 evidence files can be uploaded at a time.");
    }
}
