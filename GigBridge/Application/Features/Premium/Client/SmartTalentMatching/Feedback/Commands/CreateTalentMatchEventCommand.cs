using System;
using MediatR;

namespace Application.Features.Premium.Client.SmartTalentMatching.Feedback;

public sealed record CreateTalentMatchEventCommand(
    Guid ClientUserId,
    Guid JobPostId,
    Guid MatchRunId,
    Guid FreelancerProfileId,
    string EventType,
    string IdempotencyKey) : IRequest;
