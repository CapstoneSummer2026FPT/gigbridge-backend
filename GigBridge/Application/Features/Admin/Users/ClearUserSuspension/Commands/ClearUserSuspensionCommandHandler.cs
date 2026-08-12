using Application.Common.Interfaces;
using Application.Common.InternalServices.Accounts.Interfaces;
using Application.Features.Admin.Users.Shared.DTOs;
using AutoMapper;
using MediatR;

namespace Application.Features.Admin.Users.ClearUserSuspension.Commands;

public class ClearUserSuspensionCommandHandler : IRequestHandler<ClearUserSuspensionCommand, AdminUserDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly IUserAccountStatusService _userAccountStatusService;
    private readonly IMapper _mapper;

    public ClearUserSuspensionCommandHandler(
        IApplicationDbContext context,
        IUserAccountStatusService userAccountStatusService,
        IMapper mapper)
    {
        _context = context;
        _userAccountStatusService = userAccountStatusService;
        _mapper = mapper;
    }

    public async Task<AdminUserDto?> Handle(
        ClearUserSuspensionCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _userAccountStatusService.ClearSuspensionAsync(
            request.Request.Email,
            cancellationToken);

        if (user is null)
        {
            return null;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return _mapper.Map<AdminUserDto>(user);
    }
}
