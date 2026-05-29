using Application.Common.Interfaces.IService;
using Application.Features.Admin.Users.Shared.DTOs;
using MediatR;

namespace Application.Features.Admin.Users.GetFreelancerByEmail.Queries;

public class GetFreelancerByEmailQueryHandler : IRequestHandler<GetFreelancerByEmailQuery, AdminUserDto?>
{
    private readonly IUserService _userService;

    public GetFreelancerByEmailQueryHandler(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<AdminUserDto?> Handle(GetFreelancerByEmailQuery request, CancellationToken cancellationToken)
    {
        return await _userService.GetFreelancerByEmailAsync(request.Email, cancellationToken);
    }
}
