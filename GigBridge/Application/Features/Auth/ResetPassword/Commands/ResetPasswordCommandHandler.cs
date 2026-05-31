using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.ResetPassword.Commands;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTimeService _dateTimeService;

    public ResetPasswordCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _dateTimeService = dateTimeService;
    }

    public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var resetRequest = request.Request;
        var email = resetRequest.Email.Trim();
        var user = await _context.Set<User>()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException("Email does not exist");
        }

        EnsureResetTokenIsValid(user, resetRequest.PasswordResetToken);

        user.Password = _passwordHasher.HashPassword(resetRequest.NewPassword);
        user.EmailVerificationToken = null;
        user.TokenExpiry = null;

        await _context.SaveChangesAsync(cancellationToken);
    }

    private void EnsureResetTokenIsValid(User user, string resetToken)
    {
        if (resetToken != user.EmailVerificationToken)
        {
            throw new InvalidOperationException("wrong email verification token");
        }

        if (user.TokenExpiry < _dateTimeService.UtcNow)
        {
            throw new InvalidOperationException("Token has expired");
        }
    }
}
