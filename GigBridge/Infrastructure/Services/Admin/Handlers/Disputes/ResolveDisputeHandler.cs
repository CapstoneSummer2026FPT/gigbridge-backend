using Application.DTOs.Admin;
using Application.Features.Admin.Disputes.Resolve;
using Infrastructure.Services.Admin.Interfaces;
using MediatR;

namespace Infrastructure.Services.Admin.Handlers.Disputes;

public sealed class ResolveDisputeHandler : IRequestHandler<ResolveDisputeCommand, DisputeDetailDto>
{
    private readonly IAdminDisputeService _disputeService;

    public ResolveDisputeHandler(IAdminDisputeService disputeService)
    {
        _disputeService = disputeService;
    }

    public Task<DisputeDetailDto> Handle(ResolveDisputeCommand request, CancellationToken cancellationToken) =>
        _disputeService.ResolveAsync(request.DisputeId, request.Request, request.Actor, cancellationToken);
}
