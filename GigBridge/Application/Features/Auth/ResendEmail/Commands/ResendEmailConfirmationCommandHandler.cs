using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.ResendEmail.Commands;

public class ResendEmailConfirmationCommandHandler : IRequestHandler<ResendEmailConfirmationCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IAuthEmailSender _authEmailSender;

    public ResendEmailConfirmationCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IAuthEmailSender authEmailSender)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _authEmailSender = authEmailSender;
    }

    public async Task Handle(ResendEmailConfirmationCommand request, CancellationToken cancellationToken)
    {
        var email = request.Request.Email.Trim();
        var user = await _context.Set<User>()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("Email does not exist");
        }

        if (user.IsEmailVerified)
        {
            throw new BadRequestException("Account has already been verified");
        }

        var token = Guid.NewGuid().ToString();
        user.EmailVerificationToken = token;
        user.TokenExpiry = _dateTimeService.UtcNow.AddHours(24);

        await _context.SaveChangesAsync(cancellationToken);
        await _authEmailSender.SendVerificationEmailAsync(user.Email, token, cancellationToken);
    }
}
