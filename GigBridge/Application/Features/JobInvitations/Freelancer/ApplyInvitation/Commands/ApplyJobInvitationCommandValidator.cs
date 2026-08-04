using FluentValidation;

namespace Application.Features.JobInvitations.Freelancer.ApplyInvitation.Commands;

public sealed class ApplyJobInvitationCommandValidator : AbstractValidator<ApplyJobInvitationCommand>
{
    public ApplyJobInvitationCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.InvitationId).NotEmpty();
    }
}
