namespace Application.Features.JobPosts.Public.RecordDiscoveryEvent.DTOs;

public sealed record JobDiscoveryEventRequest(Guid EventId, Guid JobPostId, Guid? SearchEventId);
