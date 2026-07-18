using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Profiles.ClientProfile.GetClientProfile.DTOs;
using Application.Features.Profiles.ClientProfile.GetClientProfile.Queries;
using MediatR;

namespace Application.Features.Profiles.ClientProfile.GetMyClientProfile.Queries;

public class GetMyClientProfileQueryHandler
    : IRequestHandler<GetMyClientProfileQuery, ClientProfileDetailDto>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IMediator _mediator;

    public GetMyClientProfileQueryHandler(ICurrentUserService currentUserService, IMediator mediator)
    {
        _currentUserService = currentUserService;
        _mediator = mediator;
    }

    public async Task<ClientProfileDetailDto> Handle(
        GetMyClientProfileQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUserService.UserId) || !Guid.TryParse(_currentUserService.UserId, out var currentUserId))
        {
            throw new BadRequestException("User ID from token is invalid or missing.");
        }

        return await _mediator.Send(new GetClientProfileQuery(currentUserId), cancellationToken);
    }
}
