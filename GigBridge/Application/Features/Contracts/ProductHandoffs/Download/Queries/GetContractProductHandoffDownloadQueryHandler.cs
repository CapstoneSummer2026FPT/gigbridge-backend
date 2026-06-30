using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.ProductHandoffs.Common;
using Application.Features.Contracts.ProductHandoffs.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.ProductHandoffs.Download.Queries;

public sealed class GetContractProductHandoffDownloadQueryHandler :
    IRequestHandler<GetContractProductHandoffDownloadQuery, ProductHandoffDownloadResponse>
{
    private readonly IApplicationDbContext _context;

    public GetContractProductHandoffDownloadQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProductHandoffDownloadResponse> Handle(
        GetContractProductHandoffDownloadQuery query,
        CancellationToken cancellationToken)
    {
        var contract = await ContractProductHandoffAccess.GetActiveContractAsync(
            _context,
            query.ContractId,
            cancellationToken);

        await ContractProductHandoffAccess.EnsureParticipantAsync(
            _context,
            contract,
            query.UserId,
            cancellationToken);

        var handoff = await _context.Set<ContractProductHandoff>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                handoff =>
                    handoff.ContractProductHandoffId == query.HandoffId &&
                    handoff.ContractsId == query.ContractId,
                cancellationToken);

        if (handoff is null)
        {
            throw new NotFoundException("Product handoff does not exist.");
        }

        var url = handoff.SourceType == (int)ContractProductHandoffSourceType.File
            ? handoff.FileUrl
            : handoff.ExternalUrl;

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new BadRequestException("Product handoff does not have a downloadable URL.");
        }

        return new ProductHandoffDownloadResponse(
            handoff.ContractProductHandoffId,
            handoff.ContractsId,
            handoff.SourceType,
            url,
            handoff.FileName);
    }
}
