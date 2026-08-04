using FluentValidation;

namespace Application.Features.JobInvitations.Freelancer.ViewInvitation.Commands;

public sealed class ViewJobInvitationCommandValidator : AbstractValidator<ViewJobInvitationCommand>
{
    public ViewJobInvitationCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.InvitationId).NotEmpty();
    }
}
