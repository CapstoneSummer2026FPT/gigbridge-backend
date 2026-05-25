using Application.DTOs.Admin;
using Application.Features.Admin.Disputes.StartReview;
using Infrastructure.Services.Admin.Interfaces;
using MediatR;

namespace Infrastructure.Services.Admin.Handlers.Disputes;

public sealed class StartDisputeReviewHandler : IRequestHandler<StartDisputeReviewCommand, DisputeDetailDto>
{
    private readonly IAdminDisputeService _disputeService;

    public StartDisputeReviewHandler(IAdminDisputeService disputeService)
    {
        _disputeService = disputeService;
    }

    public Task<DisputeDetailDto> Handle(StartDisputeReviewCommand request, CancellationToken cancellationToken) =>
        _disputeService.ReviewAsync(request.DisputeId, request.Request, request.Actor, cancellationToken);
}
