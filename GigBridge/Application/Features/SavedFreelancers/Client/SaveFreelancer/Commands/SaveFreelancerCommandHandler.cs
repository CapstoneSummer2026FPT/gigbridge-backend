using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Application.Features.Premium.Client.SmartTalentMatching.Feedback;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SavedFreelancers.Client.SaveFreelancer.Commands;

public class SaveFreelancerCommandHandler : IRequestHandler<SaveFreelancerCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public SaveFreelancerCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(
        SaveFreelancerCommand request,
        CancellationToken cancellationToken)
    {
        var freelancerProfileExists = await _context.Set<FreelancerProfile>()
            .AnyAsync(
                x => x.FreelancerProfilesId == request.FreelancerProfileId,
                cancellationToken
            );

        if (!freelancerProfileExists)
        {
            throw new NotFoundException(nameof(FreelancerProfile), request.FreelancerProfileId);
        }

        var existingSavedFreelancer = await _context.Set<SavedFreelancer>()
            .FirstOrDefaultAsync(
                x => x.UserId == request.UserId &&
                     x.FreelancerProfilesId == request.FreelancerProfileId,
                cancellationToken
            );

        if (existingSavedFreelancer != null)
        {
            if (await AddMatchEventAsync(
                    request, existingSavedFreelancer.SavedFreelancersId, cancellationToken))
            {
                await TalentMatchFeedbackWriter.TrySaveAddedEventAsync(
                    _context, cancellationToken);
            }
            return existingSavedFreelancer.SavedFreelancersId;
        }

        var savedFreelancer = new SavedFreelancer
        {
            SavedFreelancersId = Guid.NewGuid(),
            UserId = request.UserId,
            FreelancerProfilesId = request.FreelancerProfileId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<SavedFreelancer>().Add(savedFreelancer);

        await AddMatchEventAsync(request, savedFreelancer.SavedFreelancersId, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return savedFreelancer.SavedFreelancersId;
    }

    private async Task<bool> AddMatchEventAsync(
        SaveFreelancerCommand request,
        Guid savedFreelancerId,
        CancellationToken cancellationToken)
    {
        if (!request.MatchRunId.HasValue)
        {
            return false;
        }

        return await TalentMatchFeedbackWriter.TryAddForRunAsync(
            _context,
            request.MatchRunId.Value,
            request.UserId,
            null,
            request.FreelancerProfileId,
            TalentMatchEventType.Saved,
            $"match:{request.MatchRunId.Value:N}:saved:{savedFreelancerId:N}",
            savedFreelancerId,
            DateTime.UtcNow,
            cancellationToken);
    }
}
