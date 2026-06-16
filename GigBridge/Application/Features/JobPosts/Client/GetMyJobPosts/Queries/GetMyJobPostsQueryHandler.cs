using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.JobPosts.Client.GetMyJobPosts.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.GetMyJobPosts.Queries;

public class GetMyJobPostsQueryHandler : IRequestHandler<GetMyJobPostsQuery, IEnumerable<GetMyJobPostDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMyJobPostsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<GetMyJobPostDto>> Handle(GetMyJobPostsQuery request, CancellationToken cancellationToken)
    {
        var clientProfile = await _context.Set<ClientProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.UserId == request.UserId, cancellationToken);

        if (clientProfile is null)
        {
            throw new NotFoundException("Client profile does not exist.");
        }

        return await _context.Set<JobPost>()
            .AsNoTracking()
            .Where(jobPost => jobPost.ClientProfilesId == clientProfile.ClientProfilesId)
            .OrderByDescending(jobPost => jobPost.CreatedAt)
            .Skip((NormalizePageIndex(request.PageIndex) - 1) * NormalizePageSize(request.PageSize))
            .Take(NormalizePageSize(request.PageSize))
            .Select(jobPost => new GetMyJobPostDto
            {
                JobPostsId = jobPost.JobPostsId,
                ClientProfilesId = jobPost.ClientProfilesId,
                Title = jobPost.Title,
                Description = jobPost.Description,
                CategoryId = jobPost.CategoryId,
                CategoryName = jobPost.Category != null ? jobPost.Category.Name : null,
                BudgetMin = jobPost.BudgetMin,
                BudgetMax = jobPost.BudgetMax,
                Currency = jobPost.Currency,
                EstimatedDuration = jobPost.EstimatedDuration,
                MaxHires = jobPost.MaxHires,
                Location = jobPost.Location,
                Status = jobPost.Status,
                Visibility = jobPost.Visibility,
                EndDate = jobPost.EndDate,
                IsAigenerated = jobPost.IsAigenerated,
                CreatedAt = jobPost.CreatedAt,
                UpdatedAt = jobPost.UpdatedAt,
                ProposalCount = jobPost.Proposals.Count(proposal => proposal.Status != 0)
            })
            .ToListAsync(cancellationToken);
    }

    private static int NormalizePageIndex(int pageIndex)
    {
        return pageIndex < 1 ? 1 : pageIndex;
    }

    private static int NormalizePageSize(int pageSize)
    {
        return pageSize is < 1 or > 100 ? 10 : pageSize;
    }
}
