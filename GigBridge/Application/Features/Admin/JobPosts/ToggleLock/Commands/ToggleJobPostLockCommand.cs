using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
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
        var admin = await _context.Set<User>()
            .FirstOrDefaultAsync(user => user.UserId == request.AdminUserId, cancellationToken);

        if (admin is null || admin.Role != (int)UserRole.Admin)
        {
            throw new ForbiddenAccessException("Only admins can lock or unlock job posts.");
        }

        var jobPost = await _context.Set<JobPost>()
            .FirstOrDefaultAsync(jp => jp.JobPostsId == request.JobPostId, cancellationToken);

        if (jobPost is null)
        {
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
