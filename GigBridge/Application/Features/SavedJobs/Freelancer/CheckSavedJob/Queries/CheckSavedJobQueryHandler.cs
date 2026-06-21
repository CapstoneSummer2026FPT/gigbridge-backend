using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SavedJobs.Freelancer.CheckSavedJob.Queries;

public class CheckSavedJobQueryHandler : IRequestHandler<CheckSavedJobQuery, bool>
{
    private readonly IApplicationDbContext _context;

    public CheckSavedJobQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(
        CheckSavedJobQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.Set<SavedJob>()
            .AsNoTracking()
            .AnyAsync(
                x => x.UserId == request.UserId &&
                     x.JobPostsId == request.JobPostId,
                cancellationToken
            );
    }
}