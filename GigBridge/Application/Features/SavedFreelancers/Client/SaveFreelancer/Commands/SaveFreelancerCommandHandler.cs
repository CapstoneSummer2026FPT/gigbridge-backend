using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
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

        await _context.SaveChangesAsync(cancellationToken);

        return savedFreelancer.SavedFreelancersId;
    }
}