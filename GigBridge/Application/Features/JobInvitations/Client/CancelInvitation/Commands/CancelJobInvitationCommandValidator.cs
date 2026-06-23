using FluentValidation;

namespace Application.Features.JobInvitations.Client.CancelInvitation.Commands;

public sealed class CancelJobInvitationCommandValidator : AbstractValidator<CancelJobInvitationCommand>
{
    public CancelJobInvitationCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.InvitationId).NotEmpty();
    }
}
