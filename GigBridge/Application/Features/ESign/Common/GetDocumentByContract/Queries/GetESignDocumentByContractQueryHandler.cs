using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.ESign.Common.DTOs;
using Application.Features.ESign.Common.Internal;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ESign.Common.GetDocumentByContract.Queries;

public sealed class GetESignDocumentByContractQueryHandler
    : IRequestHandler<GetESignDocumentByContractQuery, ESignDocumentStatusResponse>
{
    private readonly IApplicationDbContext _context;

    public GetESignDocumentByContractQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ESignDocumentStatusResponse> Handle(
        GetESignDocumentByContractQuery request,
        CancellationToken cancellationToken)
    {
        var document = await _context.Set<EsignDocument>()
            .Where(d => d.ContractsId == request.ContractId)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (document == null)
        {
            throw new NotFoundException("E-sign document does not exist for this contract.");
        }

        await ESignAccessGuard.EnsureCanViewDocumentAsync(
            _context,
            document,
            request.UserId,
            cancellationToken);

        return await ESignDocumentProjection.ToStatusResponseAsync(
            _context,
            document,
            request.UserId,
            cancellationToken);
    }
}
