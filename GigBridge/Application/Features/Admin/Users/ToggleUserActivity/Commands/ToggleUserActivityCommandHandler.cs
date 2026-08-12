using Application.Common.Interfaces;
using Application.Common.InternalServices.Accounts.Interfaces;
using MediatR;

namespace Application.Features.Admin.Users.ToggleUserActivity.Commands;

public class ToggleUserActivityCommandHandler : IRequestHandler<ToggleUserActivityCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IUserAccountStatusService _userAccountStatusService;

    public ToggleUserActivityCommandHandler(
        IApplicationDbContext context,
        IUserAccountStatusService userAccountStatusService)
    {
        _context = context;
        _userAccountStatusService = userAccountStatusService;
    }

    public async Task<bool> Handle(ToggleUserActivityCommand request, CancellationToken cancellationToken)
    {
        var user = await _userAccountStatusService.ToggleActiveAsync(request.Email, cancellationToken);

        if (user is null)
        {
            return false;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
