using Application.Common.Interfaces.IService;
using MediatR;

namespace Application.Features.Admin.Users.DeleteUser.Commands;

public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, bool>
{
    private readonly IUserService _userService;

    public DeleteUserCommandHandler(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<bool> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        return await _userService.DeleteAsync(request.Email, cancellationToken);
    }
}
