using Application.Features.Admin.Users.Command;
using Application.Features.Admin.Users.Dto;
using Infrastructure.Services.Admin.Interfaces;
using MediatR;

namespace Infrastructure.Services.Admin.Handlers.Users;

public sealed class ChangeUserStatusHandler : IRequestHandler<ChangeUserStatusCommand, AdminUserDetailDto>
{
    private readonly IAdminUserService _userService;

    public ChangeUserStatusHandler(IAdminUserService userService)
    {
        _userService = userService;
    }

    public Task<AdminUserDetailDto> Handle(ChangeUserStatusCommand request, CancellationToken cancellationToken) =>
        _userService.SetStatusAsync(request.UserId, request.Request, request.Actor, cancellationToken);
}

