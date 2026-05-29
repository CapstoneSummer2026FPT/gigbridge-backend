using Application.Common.Interfaces.IService;
using Application.Features.Admin.Users.GetAllUser.DTOs;
using MediatR;

namespace Application.Features.Admin.Users.GetAllUser.Queries;

public class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, GetAllUsersResponse>
{
    private readonly IUserService _userService;

    public GetAllUsersQueryHandler(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<GetAllUsersResponse> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        return await _userService.GetAllAsync(request.Page, request.PageSize, request.Search, request.Status, cancellationToken);
    }
}
