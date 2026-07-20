using FluentValidation;

namespace Application.Features.ReportContracts.Confirm.Commands;

public sealed class ConfirmResolutionCommandValidator : AbstractValidator<ConfirmResolutionCommand>
{
    public ConfirmResolutionCommandValidator()
    {
        RuleFor(v => v.IsAccepted)
            .NotNull()
            .WithMessage("You must accept or decline the resolution.");
    }
}
