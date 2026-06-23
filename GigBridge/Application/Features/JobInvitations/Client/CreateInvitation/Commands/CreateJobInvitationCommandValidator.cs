using FluentValidation;

namespace Application.Features.JobInvitations.Client.CreateInvitation.Commands;

public sealed class CreateJobInvitationCommandValidator : AbstractValidator<CreateJobInvitationCommand>
{
    public CreateJobInvitationCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.Request).NotNull();
        RuleFor(command => command.Request.JobPostId).NotEmpty();
        RuleFor(command => command.Request.FreelancerProfileId).NotEmpty();
        RuleFor(command => command.Request.Message).MaximumLength(1000);
        RuleFor(command => command.Request.ExpiresAt)
            .Must(expiresAt => !expiresAt.HasValue || expiresAt.Value > DateTime.UtcNow)
            .WithMessage("Expiration date must be in the future.");
    }
}
