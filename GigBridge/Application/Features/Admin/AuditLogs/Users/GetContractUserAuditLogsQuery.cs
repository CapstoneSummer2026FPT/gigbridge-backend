using Application.Common.Interfaces;
using Application.Features.Admin.Disputes.Common.DTOs;
using Application.Features.Admin.Disputes.Common.Internal;
using MediatR;

namespace Application.Features.Admin.AuditLogs.Users;

public sealed record GetContractUserAuditLogsQuery(Guid ContractId)
    : IRequest<IReadOnlyList<AdminUserAuditEventResponse>>;

public sealed class GetContractUserAuditLogsQueryHandler :
    IRequestHandler<GetContractUserAuditLogsQuery, IReadOnlyList<AdminUserAuditEventResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetContractUserAuditLogsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public Task<IReadOnlyList<AdminUserAuditEventResponse>> Handle(
        GetContractUserAuditLogsQuery request,
        CancellationToken cancellationToken) =>
        UserAuditLogQueries.GetForContractAsync(_context, request.ContractId, cancellationToken);
}
