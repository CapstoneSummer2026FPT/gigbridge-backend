using Application.Common.Interfaces;
using Application.Features.SavedFreelancers.Client.GetMySavedFreelancers.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SavedFreelancers.Client.GetMySavedFreelancers.Queries;

public class GetMySavedFreelancersQueryHandler
    : IRequestHandler<GetMySavedFreelancersQuery, IEnumerable<SavedFreelancerDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMySavedFreelancersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<SavedFreelancerDto>> Handle(
        GetMySavedFreelancersQuery request,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : Math.Min(request.PageSize, 100);

        var savedFreelancers = await _context.Set<SavedFreelancer>()
            .AsNoTracking()
            .Where(x => x.UserId == request.UserId)
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SavedFreelancerDto
            {
                SavedFreelancerId = x.SavedFreelancersId,

                FreelancerProfileId = x.FreelancerProfilesId,
                FreelancerUserId = x.FreelancerProfiles.UserId,

                Title = x.FreelancerProfiles.Title,
                Bio = x.FreelancerProfiles.Bio,
                Availability = x.FreelancerProfiles.Availability,
                Location = x.FreelancerProfiles.Location,
                ProfileCompletionScore = x.FreelancerProfiles.ProfileCompletionScore,

                FreelancerCreatedAt = x.FreelancerProfiles.CreatedAt,
                SavedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return savedFreelancers;
    }
}