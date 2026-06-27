using FluentValidation;

namespace Application.Features.Proposals.Freelancer.Cheating.Commands;

public class LogProposalCheatingEventCommandValidator : AbstractValidator<LogProposalCheatingEventCommand>
{
    public LogProposalCheatingEventCommandValidator()
    {
        RuleFor(command => command.ProposalId).NotEmpty();
        RuleFor(command => command.FreelancerUserId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.ClientEventId)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(command => command.Request.EventType)
                .InclusiveBetween(0, 5);

            RuleFor(command => command.Request.Metadata)
                .Must(metadata => metadata is null || metadata.Count <= 20)
                .WithMessage("Metadata must not contain more than 20 entries.");
        });
    }
}
