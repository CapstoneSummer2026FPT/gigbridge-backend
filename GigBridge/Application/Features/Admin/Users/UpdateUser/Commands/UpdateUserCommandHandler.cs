using Application.Common.Interfaces.IService;
using Application.Features.Admin.Users.Shared.DTOs;
using MediatR;

namespace Application.Features.Admin.Users.UpdateUser.Commands;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, AdminUserDto?>
{
    private readonly IUserService _userService;

    public UpdateUserCommandHandler(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<AdminUserDto?> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        return await _userService.UpdateAsync(request.Email, request.Request, cancellationToken);
    }
}
