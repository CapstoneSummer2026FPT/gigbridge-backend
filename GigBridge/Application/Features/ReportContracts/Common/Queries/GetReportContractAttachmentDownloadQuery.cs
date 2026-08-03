using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.ReportContracts.Common.Internal;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ReportContracts.Common.Queries;

public sealed record GetReportContractAttachmentDownloadQuery(Guid ContractId, Guid ReportId, Guid AttachmentId, Guid UserId) : IRequest<ReportContractAttachmentDownloadResponse>;
public sealed record ReportContractAttachmentDownloadResponse(Guid AttachmentId, string FileName, string DownloadUrl);

public sealed class GetReportContractAttachmentDownloadQueryHandler : IRequestHandler<GetReportContractAttachmentDownloadQuery, ReportContractAttachmentDownloadResponse>
{
    private readonly IApplicationDbContext _context;
    public GetReportContractAttachmentDownloadQueryHandler(IApplicationDbContext context) => _context = context;
    public async Task<ReportContractAttachmentDownloadResponse> Handle(GetReportContractAttachmentDownloadQuery q, CancellationToken ct)
    {
        var contract = await ReportContractAccess.GetContractAsync(_context, q.ContractId, ct);
        await ReportContractAccess.EnsureParticipantAsync(_context, contract, q.UserId, ct);
        var attachment = await _context.Set<ReportContractAttachment>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.ReportContractId == q.ReportId && x.ReportContractAttachmentId == q.AttachmentId, ct)
            ?? throw new NotFoundException("Contract Report attachment does not exist.");
        var belongs = await _context.Set<ReportContract>().AsNoTracking().AnyAsync(x => x.ReportContractId == q.ReportId && x.ContractId == q.ContractId, ct);
        if (!belongs) throw new NotFoundException("Contract Report does not exist for this Contract.");
        return new(attachment.ReportContractAttachmentId, attachment.FileName, attachment.FileUrl);
    }
}
