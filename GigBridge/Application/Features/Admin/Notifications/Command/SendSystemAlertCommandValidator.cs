using FluentValidation;

namespace Application.Features.Admin.Notifications.Command;

public sealed class SendSystemAlertCommandValidator : AbstractValidator<SendSystemAlertCommand>
{
    public SendSystemAlertCommandValidator()
    {
        RuleFor(command => command.Request.Title)
            .NotEmpty()
            .WithMessage("A notification title is required.");

        RuleFor(command => command.Request.UserIds)
            .NotEmpty()
            .WithMessage("At least one recipient identifier is required.");
    }
}
