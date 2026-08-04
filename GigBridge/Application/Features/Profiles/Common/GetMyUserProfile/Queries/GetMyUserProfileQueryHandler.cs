using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Profiles.Common.DTOs;
using Application.Features.Profiles.Common.GetUserProfile.Queries;
using MediatR;

namespace Application.Features.Profiles.Common.GetMyUserProfile.Queries;

public sealed class GetMyUserProfileQueryHandler
    : IRequestHandler<GetMyUserProfileQuery, UserProfileDto>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IMediator _mediator;

    public GetMyUserProfileQueryHandler(
        ICurrentUserService currentUserService,
        IMediator mediator)
    {
        _currentUserService = currentUserService;
        _mediator = mediator;
    }

    public async Task<UserProfileDto> Handle(
        GetMyUserProfileQuery request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUserService.UserId, out var currentUserId))
        {
            throw new BadRequestException("User ID from token is invalid or missing.");
        }

        return await _mediator.Send(new GetUserProfileQuery(currentUserId), cancellationToken);
    }
}
