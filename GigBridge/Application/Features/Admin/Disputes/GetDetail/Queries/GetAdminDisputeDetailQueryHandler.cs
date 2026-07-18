using Application.Common.Interfaces;
using Application.Features.Admin.Disputes.Common.DTOs;
using Application.Features.Admin.Disputes.Common.Internal;
using MediatR;

namespace Application.Features.Admin.Disputes.GetDetail.Queries;

public sealed class GetAdminDisputeDetailQueryHandler :
    IRequestHandler<GetAdminDisputeDetailQuery, AdminDisputeDetailResponse>
{
    private readonly IApplicationDbContext _context;

    public GetAdminDisputeDetailQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public Task<AdminDisputeDetailResponse> Handle(
        GetAdminDisputeDetailQuery request,
        CancellationToken cancellationToken) =>
        AdminDisputeSupport.GetDetailAsync(_context, request.DisputeId, cancellationToken);
}
