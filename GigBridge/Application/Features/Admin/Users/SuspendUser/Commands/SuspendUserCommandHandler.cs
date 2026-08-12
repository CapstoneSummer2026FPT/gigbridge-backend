using Application.Common.Interfaces;
using Application.Common.InternalServices.Accounts.Interfaces;
using Application.Features.Admin.Users.Shared.DTOs;
using AutoMapper;
using MediatR;

namespace Application.Features.Admin.Users.SuspendUser.Commands;

public class SuspendUserCommandHandler : IRequestHandler<SuspendUserCommand, AdminUserDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly IUserAccountStatusService _userAccountStatusService;
    private readonly IMapper _mapper;

    public SuspendUserCommandHandler(
        IApplicationDbContext context,
        IUserAccountStatusService userAccountStatusService,
        IMapper mapper)
    {
        _context = context;
        _userAccountStatusService = userAccountStatusService;
        _mapper = mapper;
    }

    public async Task<AdminUserDto?> Handle(SuspendUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userAccountStatusService.SuspendAsync(
            request.Request.Email,
            request.Request.SuspendedUntil,
            request.Request.Reason,
            cancellationToken);

        if (user is null)
        {
            return null;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return _mapper.Map<AdminUserDto>(user);
    }
}
