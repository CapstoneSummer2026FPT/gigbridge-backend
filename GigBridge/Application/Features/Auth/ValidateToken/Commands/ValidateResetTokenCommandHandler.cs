using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.ValidateToken.Commands;

public class ValidateResetTokenCommandHandler : IRequestHandler<ValidateResetTokenCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public ValidateResetTokenCommandHandler(IApplicationDbContext context, IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<bool> Handle(ValidateResetTokenCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.EmailVerificationToken == request.Request.Token, cancellationToken);

        return user is null || user.TokenExpiry < _dateTimeService.UtcNow;
    }
}
