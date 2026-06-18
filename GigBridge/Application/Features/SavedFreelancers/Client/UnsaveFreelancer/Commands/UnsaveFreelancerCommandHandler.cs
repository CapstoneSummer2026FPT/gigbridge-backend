using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SavedFreelancers.Client.UnsaveFreelancer.Commands;

public class UnsaveFreelancerCommandHandler : IRequestHandler<UnsaveFreelancerCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public UnsaveFreelancerCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(
        UnsaveFreelancerCommand request,
        CancellationToken cancellationToken)
    {
        var savedFreelancer = await _context.Set<SavedFreelancer>()
            .FirstOrDefaultAsync(
                x => x.UserId == request.UserId &&
                     x.FreelancerProfilesId == request.FreelancerProfileId,
                cancellationToken
            );

        if (savedFreelancer == null)
        {
            return Unit.Value;
        }

        _context.Set<SavedFreelancer>().Remove(savedFreelancer);

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}