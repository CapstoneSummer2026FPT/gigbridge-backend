using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Elo.Common;

public static class AdminEloSupport
{
    /// <summary>
    /// Guards every admin Elo operation: the caller must be an active Admin
    /// account. Mirrors the pattern used by other admin feature areas.
    /// </summary>
    public static async Task EnsureAdminAsync(
        IApplicationDbContext context,
        Guid adminId,
        CancellationToken cancellationToken)
    {
        var isAdmin = await context.Set<User>()
            .AsNoTracking()
            .AnyAsync(user =>
                user.UserId == adminId &&
                user.Role == (int)UserRole.Admin &&
                user.IsActive,
                cancellationToken);

        if (!isAdmin)
            throw new ForbiddenAccessException("An active administrator account is required.");
    }
}
