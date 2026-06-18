using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.ESign.Common.DTOs;
using Application.Features.ESign.Common.Internal;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ESign.Documents.GetByJob.Queries;

public sealed class GetESignDocumentByJobPostQueryHandler
    : IRequestHandler<GetESignDocumentByJobPostQuery, ESignDocumentResponse>
{
    private readonly IApplicationDbContext _context;

    public GetESignDocumentByJobPostQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ESignDocumentResponse> Handle(
        GetESignDocumentByJobPostQuery request,
        CancellationToken cancellationToken)
    {
        var jobPost = await ESignAccessGuard.GetJobPostAsync(
            _context,
            request.JobPostId,
            cancellationToken);

        await ESignAccessGuard.EnsureOwningClientAsync(
            _context,
            jobPost,
            request.UserId,
            cancellationToken);

        var document = await _context.Set<EsignDocument>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                document =>
                    document.JobPostsId == request.JobPostId &&
                    document.ContractsId == null,
                cancellationToken);

        if (document is null)
        {
            throw new NotFoundException("E-sign document does not exist for this job post.");
        }

        return await ESignDocumentProjection.ToResponseAsync(_context, document, cancellationToken);
    }
}
