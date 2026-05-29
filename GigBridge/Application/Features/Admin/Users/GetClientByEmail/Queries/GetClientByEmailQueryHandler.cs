using Application.Common.Interfaces.IService;
using Application.Features.Admin.Users.Shared.DTOs;
using MediatR;

namespace Application.Features.Admin.Users.GetClientByEmail.Queries;

public class GetClientByEmailQueryHandler : IRequestHandler<GetClientByEmailQuery, AdminUserDto?>
{
    private readonly IUserService _userService;

    public GetClientByEmailQueryHandler(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<AdminUserDto?> Handle(GetClientByEmailQuery request, CancellationToken cancellationToken)
    {
        return await _userService.GetClientByEmailAsync(request.Email, cancellationToken);
    }
}
