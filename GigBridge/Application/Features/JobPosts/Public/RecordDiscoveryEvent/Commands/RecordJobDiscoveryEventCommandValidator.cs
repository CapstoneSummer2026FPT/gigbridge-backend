using FluentValidation;

namespace Application.Features.JobPosts.Public.RecordDiscoveryEvent.Commands;

public sealed class RecordJobDiscoveryEventCommandValidator : AbstractValidator<RecordJobDiscoveryEventCommand>
{
    public RecordJobDiscoveryEventCommandValidator()
    {
        RuleFor(x => x.ActorIdentity).NotEmpty().MaximumLength(80);
        RuleFor(x => x.EventId).NotEmpty();
        RuleFor(x => x.JobPostId).NotEmpty();
    }
}
