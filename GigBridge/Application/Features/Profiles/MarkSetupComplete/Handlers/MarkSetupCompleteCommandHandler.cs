using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Profiles.MarkSetupComplete.Handlers;

public class MarkSetupCompleteCommandHandler : IRequestHandler<Commands.MarkSetupCompleteCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public MarkSetupCompleteCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(Commands.MarkSetupCompleteCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUserService.UserId) || !Guid.TryParse(_currentUserService.UserId, out var currentUserId))
        {
            throw new BadRequestException("User ID from token is invalid or missing.");
        }

        var user = await _context.Set<User>().FirstOrDefaultAsync(u => u.UserId == currentUserId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException(nameof(User), currentUserId);
        }

        user.IsSetup = true;
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
