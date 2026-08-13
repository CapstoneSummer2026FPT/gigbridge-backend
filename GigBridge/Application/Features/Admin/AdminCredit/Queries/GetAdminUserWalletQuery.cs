using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Wallets.Common;
using Application.Features.Wallets.Common.DTOs;
using Domain.Entities;
using Domain.Enums.Accounts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.AdminCredit.Queries;

public sealed record GetAdminUserWalletQuery(
    Guid AdminUserId,
    Guid TargetUserId) : IRequest<WalletResponse>;

public sealed class GetAdminUserWalletQueryHandler :
    IRequestHandler<GetAdminUserWalletQuery, WalletResponse>
{
    private readonly IApplicationDbContext _context;

    public GetAdminUserWalletQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WalletResponse> Handle(
        GetAdminUserWalletQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _context.Set<User>()
            .FirstOrDefaultAsync(user => user.UserId == request.AdminUserId, cancellationToken);

        if (admin is null || admin.Role != (int)UserRole.Admin)
        {
            throw new ForbiddenAccessException("Only admins can access user wallet information.");
        }

        var targetUserExists = await _context.Set<User>()
            .AnyAsync(user => user.UserId == request.TargetUserId, cancellationToken);

        if (!targetUserExists)
        {
            throw new NotFoundException("Target user does not exist.");
        }

        var wallet = await WalletWorkflow.GetOrCreateWalletAsync(
            _context,
            request.TargetUserId,
            DateTime.UtcNow,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return WalletResponse.FromEntity(wallet);
    }
}
