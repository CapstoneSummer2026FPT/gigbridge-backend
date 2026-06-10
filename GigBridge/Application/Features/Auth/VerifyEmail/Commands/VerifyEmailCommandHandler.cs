using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.VerifyEmail.Commands;

public class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public VerifyEmailCommandHandler(IApplicationDbContext context, IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Set<User>()
            .FirstOrDefaultAsync(u => u.EmailVerificationToken == request.VerifyEmailRequest.Token, cancellationToken);

        if (user is null)
        {
            throw new BadRequestException("Invalid token");
        }

        if (user.TokenExpiry < _dateTimeService.UtcNow)
        {
            throw new BadRequestException("Token has expired");
        }

        user.IsEmailVerified = true;
        user.EmailVerificationToken = null;
        user.TokenExpiry = null;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
