using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.ChangePassword.Commands;

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserService _currentUserService;

    public ChangePasswordCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _currentUserService = currentUserService;
    }

    public async Task Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUserService.UserId) || !Guid.TryParse(_currentUserService.UserId, out var currentUserId))
        {
            throw new BadRequestException("User ID from token is invalid or missing.");
        }

        var user = await _context.Set<User>()
            .FirstOrDefaultAsync(u => u.UserId == currentUserId, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException(nameof(User), currentUserId);
        }

        if (!_passwordHasher.VerifyPassword(request.Request.CurrentPassword, user.Password ?? string.Empty))
        {
            throw new BadRequestException("Current password is incorrect.");
        }

        if (request.Request.CurrentPassword == request.Request.NewPassword)
        {
            throw new BadRequestException("New password cannot be the same as current password.");
        }

        user.Password = _passwordHasher.HashPassword(request.Request.NewPassword);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
