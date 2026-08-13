using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums.Accounts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.JobPosts.ToggleLock.Commands;

public sealed record ToggleJobPostLockCommand(
    Guid AdminUserId,
    Guid JobPostId) : IRequest<bool>;

public sealed class ToggleJobPostLockCommandHandler :
    IRequestHandler<ToggleJobPostLockCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public ToggleJobPostLockCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(
        ToggleJobPostLockCommand request,
        CancellationToken cancellationToken)
    {
        var jobPost = await _context.Set<JobPost>()
            .Where(jp => jp.JobPostsId == request.JobPostId)
            .Where(_ => _context.Set<User>().Any(user =>
                user.UserId == request.AdminUserId && user.Role == (int)UserRole.Admin))
            .FirstOrDefaultAsync(cancellationToken);

        if (jobPost is null)
        {
            var isAdmin = await _context.Set<User>()
                .AsNoTracking()
                .AnyAsync(user =>
                    user.UserId == request.AdminUserId && user.Role == (int)UserRole.Admin,
                    cancellationToken);

            if (!isAdmin)
            {
                throw new ForbiddenAccessException("Only admins can lock or unlock job posts.");
            }

            throw new NotFoundException("Job post does not exist.");
        }

        // Toggle logic: Visibility = 3 means Locked by Admin
        if (jobPost.Visibility == 3)
        {
            jobPost.Visibility = 0; // Reset to Public
        }
        else
        {
            jobPost.Visibility = 3; // Lock it
        }

        jobPost.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return jobPost.Visibility == 3;
    }
}
