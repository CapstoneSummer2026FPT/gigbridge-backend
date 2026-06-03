using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.Queries;
using MediatR;

namespace Application.Features.Profiles.FreelancerProfile.GetMyFreelancerProfile.Handlers;

public class GetMyFreelancerProfileQueryHandler 
    : IRequestHandler<Queries.GetMyFreelancerProfileQuery, FreelancerProfileDetailDto>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IMediator _mediator;

    public GetMyFreelancerProfileQueryHandler(ICurrentUserService currentUserService, IMediator mediator)
    {
        _currentUserService = currentUserService;
        _mediator = mediator;
    }

    public async Task<FreelancerProfileDetailDto> Handle(
        Queries.GetMyFreelancerProfileQuery request, 
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUserService.UserId) || !Guid.TryParse(_currentUserService.UserId, out var currentUserId))
        {
            throw new BadRequestException("User ID from token is invalid or missing.");
        }

        return await _mediator.Send(new GetFreelancerProfileQuery(currentUserId), cancellationToken);
    }
}
