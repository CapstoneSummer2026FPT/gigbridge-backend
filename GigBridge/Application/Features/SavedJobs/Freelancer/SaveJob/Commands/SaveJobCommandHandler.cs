using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SavedJobs.Freelancer.SaveJob.Commands;

public class SaveJobCommandHandler : IRequestHandler<SaveJobCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public SaveJobCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(SaveJobCommand request, CancellationToken cancellationToken)
    {
        var jobExists = await _context.Set<JobPost>()
            .AnyAsync(
                x => x.JobPostsId == request.JobPostId,
                cancellationToken
            );

        if (!jobExists)
        {
            throw new NotFoundException(nameof(JobPost), request.JobPostId);
        }

        var existingSavedJob = await _context.Set<SavedJob>()
            .FirstOrDefaultAsync(
                x => x.UserId == request.UserId &&
                     x.JobPostsId == request.JobPostId,
                cancellationToken
            );

        if (existingSavedJob != null)
        {
            return existingSavedJob.SavedJobsId;
        }

        var savedJob = new SavedJob
        {
            SavedJobsId = Guid.NewGuid(),
            UserId = request.UserId,
            JobPostsId = request.JobPostId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<SavedJob>().Add(savedJob);

        await _context.SaveChangesAsync(cancellationToken);

        return savedJob.SavedJobsId;
    }
}