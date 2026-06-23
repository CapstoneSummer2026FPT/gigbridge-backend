using FluentValidation;

namespace Application.Features.JobInvitations.Client.BulkCreateInvitations.Commands;

public sealed class BulkCreateJobInvitationsCommandValidator : AbstractValidator<BulkCreateJobInvitationsCommand>
{
    public BulkCreateJobInvitationsCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.Request).NotNull();
        RuleFor(command => command.Request.JobPostIds)
            .NotEmpty()
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("Job post ids must not contain empty values.");
        RuleFor(command => command.Request.FreelancerProfileIds)
            .NotEmpty()
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("Freelancer profile ids must not contain empty values.");
        RuleFor(command => command.Request.Message).MaximumLength(1000);
        RuleFor(command => command.Request.ExpiresAt)
            .Must(expiresAt => !expiresAt.HasValue || expiresAt.Value > DateTime.UtcNow)
            .WithMessage("Expiration date must be in the future.");
    }
}
