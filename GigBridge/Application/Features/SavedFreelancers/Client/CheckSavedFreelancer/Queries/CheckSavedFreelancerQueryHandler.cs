using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SavedFreelancers.Client.CheckSavedFreelancer.Queries;

public class CheckSavedFreelancerQueryHandler
    : IRequestHandler<CheckSavedFreelancerQuery, bool>
{
    private readonly IApplicationDbContext _context;

    public CheckSavedFreelancerQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(
        CheckSavedFreelancerQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.Set<SavedFreelancer>()
            .AsNoTracking()
            .AnyAsync(
                x => x.UserId == request.UserId &&
                     x.FreelancerProfilesId == request.FreelancerProfileId,
                cancellationToken
            );
    }
}