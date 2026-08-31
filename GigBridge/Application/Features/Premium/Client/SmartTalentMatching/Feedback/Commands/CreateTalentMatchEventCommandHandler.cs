using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Premium.Interfaces;
using Domain.Entities;
using Domain.Enums.Premium;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Client.SmartTalentMatching.Feedback;

public sealed class CreateTalentMatchEventCommandHandler(
    IApplicationDbContext context,
    IPremiumAccessService premiumAccess,
    IDateTimeService clock)
    : IRequestHandler<CreateTalentMatchEventCommand>
{
    public async Task Handle(CreateTalentMatchEventCommand request, CancellationToken cancellationToken)
    {
        await premiumAccess.RequirePremiumClientAsync(request.ClientUserId, cancellationToken);
        var eventType = ParseEventType(request.EventType);
        var belongsToRun = await context.Set<TalentMatchResult>()
            .AsNoTracking()
            .AnyAsync(result =>
                    result.TalentMatchRunId == request.MatchRunId &&
                    result.FreelancerProfileId == request.FreelancerProfileId &&
                    result.TalentMatchRun.ClientUserId == request.ClientUserId &&
                    result.TalentMatchRun.JobPostId == request.JobPostId,
                cancellationToken);
        if (!belongsToRun)
        {
            throw new NotFoundException("Talent match result not found for this client and job.");
        }

        var added = await TalentMatchFeedbackWriter.TryAddForRunAsync(
            context,
            request.MatchRunId,
            request.ClientUserId,
            request.JobPostId,
            request.FreelancerProfileId,
            eventType,
            request.IdempotencyKey,
            null,
            clock.UtcNow,
            cancellationToken);
        if (added)
        {
            await TalentMatchFeedbackWriter.TrySaveAddedEventAsync(context, cancellationToken);
        }
    }

    private static TalentMatchEventType ParseEventType(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "impression" => TalentMatchEventType.Impression,
            "profile_open" or "profile_opened" => TalentMatchEventType.ProfileOpened,
            _ => throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(CreateTalentMatchEventCommand.EventType)] =
                    ["Only impression and profile_opened events may be recorded by the client."]
            })
        };
}
