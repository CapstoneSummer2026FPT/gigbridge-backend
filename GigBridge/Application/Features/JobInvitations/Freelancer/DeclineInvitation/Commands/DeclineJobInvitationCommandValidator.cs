using FluentValidation;

namespace Application.Features.JobInvitations.Freelancer.DeclineInvitation.Commands;

public sealed class DeclineJobInvitationCommandValidator : AbstractValidator<DeclineJobInvitationCommand>
{
    public DeclineJobInvitationCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.InvitationId).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(500);
    }
}
