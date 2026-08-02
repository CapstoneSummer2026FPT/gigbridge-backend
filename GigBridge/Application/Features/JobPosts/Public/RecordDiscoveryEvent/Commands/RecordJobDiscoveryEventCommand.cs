using MediatR;

namespace Application.Features.JobPosts.Public.RecordDiscoveryEvent.Commands;

public sealed record RecordJobDiscoveryEventCommand(
    string ActorIdentity,
    Guid EventId,
    Guid JobPostId,
    Guid? SearchEventId) : IRequest<RecordJobDiscoveryEventResult>;

public sealed record RecordJobDiscoveryEventResult(bool Accepted);
