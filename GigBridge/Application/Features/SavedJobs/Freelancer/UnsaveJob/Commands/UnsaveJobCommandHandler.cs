using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SavedJobs.Freelancer.UnsaveJob.Commands;

public class UnsaveJobCommandHandler : IRequestHandler<UnsaveJobCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public UnsaveJobCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(UnsaveJobCommand request, CancellationToken cancellationToken)
    {
        var savedJob = await _context.Set<SavedJob>()
            .FirstOrDefaultAsync(
                x => x.UserId == request.UserId &&
                     x.JobPostsId == request.JobPostId,
                cancellationToken
            );

        if (savedJob == null)
        {
            return Unit.Value;
        }

        _context.Set<SavedJob>().Remove(savedJob);

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}