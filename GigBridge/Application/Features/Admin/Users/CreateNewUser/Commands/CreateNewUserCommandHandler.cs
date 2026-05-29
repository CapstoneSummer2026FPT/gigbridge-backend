using Application.Common.Interfaces.IService;
using Application.Features.Admin.Users.Shared.DTOs;
using MediatR;

namespace Application.Features.Admin.Users.CreateNewUser.Commands;

public class CreateNewUserCommandHandler : IRequestHandler<CreateNewUserCommand, AdminUserDto>
{
    private readonly IUserService _userService;

    public CreateNewUserCommandHandler(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<AdminUserDto> Handle(CreateNewUserCommand request, CancellationToken cancellationToken)
    {
        return await _userService.CreateAsync(request.Request, cancellationToken);
    }
}
