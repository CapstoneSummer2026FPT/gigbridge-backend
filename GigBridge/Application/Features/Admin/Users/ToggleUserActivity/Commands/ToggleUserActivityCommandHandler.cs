using Application.Common.Interfaces.IService;
using MediatR;

namespace Application.Features.Admin.Users.ToggleUserActivity.Commands;

public class ToggleUserActivityCommandHandler : IRequestHandler<ToggleUserActivityCommand, bool>
{
    private readonly IUserService _userService;

    public ToggleUserActivityCommandHandler(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<bool> Handle(ToggleUserActivityCommand request, CancellationToken cancellationToken)
    {
        return await _userService.ToggleActivityAsync(request.Email, cancellationToken);
    }
}
