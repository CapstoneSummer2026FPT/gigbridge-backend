using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Application.Features.Auth.ForgotPassword.Commands;

public class SendEmailPasswordChangingCommandHandler : IRequestHandler<SendEmailPasswordChangingCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly IAuthEmailSender _authEmailSender;

    public SendEmailPasswordChangingCommandHandler(
        IApplicationDbContext context,
        ICacheService cacheService,
        IAuthEmailSender authEmailSender)
    {
        _context = context;
        _cacheService = cacheService;
        _authEmailSender = authEmailSender;
    }

    public async Task Handle(SendEmailPasswordChangingCommand request, CancellationToken cancellationToken)
    {
        var email = request.Request.Email.Trim().ToLowerInvariant();
        var user = await _context.Set<User>()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException("Email does not exist");
        }

        var cacheKey = $"otp:{email}";
        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        await _cacheService.RemoveAsync(cacheKey, cancellationToken);
        await _cacheService.SetAsync(cacheKey, otp, TimeSpan.FromMinutes(5), cancellationToken);
        await _authEmailSender.SendOtpEmailAsync(user.Email, otp, cancellationToken);
    }
}
