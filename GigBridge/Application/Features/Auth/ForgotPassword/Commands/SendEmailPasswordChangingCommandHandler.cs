using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.ForgotPassword.Commands;

public class SendEmailPasswordChangingCommandHandler : IRequestHandler<SendEmailPasswordChangingCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IAuthEmailSender _authEmailSender;

    public SendEmailPasswordChangingCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IAuthEmailSender authEmailSender)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _authEmailSender = authEmailSender;
    }

    public async Task Handle(SendEmailPasswordChangingCommand request, CancellationToken cancellationToken)
    {
        var email = request.Request.Email.Trim();
        var user = await _context.Set<User>()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException("Email does not exist");
        }

        var token = Guid.NewGuid().ToString();
        user.EmailVerificationToken = token;
        user.TokenExpiry = _dateTimeService.UtcNow.AddHours(1);

        await _context.SaveChangesAsync(cancellationToken);
        await _authEmailSender.SendPasswordResetEmailAsync(user.Email, token, cancellationToken);
    }
}
