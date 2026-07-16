using FluentValidation;

namespace Application.Features.Disputes.Create.Commands;

public sealed class CreateDisputeCommandValidator : AbstractValidator<CreateDisputeCommand>
{
    private const int ReasonMaxLength = 2000;

    public CreateDisputeCommandValidator()
    {
        RuleFor(command => command.ContractId)
            .NotEmpty();

        RuleFor(command => command.Reason)
            .NotEmpty()
            .WithMessage("Dispute reason is required.")
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("Dispute reason cannot be only whitespace.")
            .MaximumLength(ReasonMaxLength)
            .WithMessage($"Dispute reason must not exceed {ReasonMaxLength} characters.");
    }
}
