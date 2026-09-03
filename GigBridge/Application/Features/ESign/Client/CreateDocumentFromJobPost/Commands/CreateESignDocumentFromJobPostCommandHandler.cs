using Application.Common.InternalServices.ESign.Services;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Features.ESign.Common.DTOs;
using Application.Features.ESign.Common.Internal;
using MediatR;

namespace Application.Features.ESign.Client.CreateDocumentFromJobPost.Commands;

public sealed class CreateESignDocumentFromJobPostCommandHandler
    : IRequestHandler<CreateESignDocumentFromJobPostCommand, ESignDocumentResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public CreateESignDocumentFromJobPostCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<ESignDocumentResponse> Handle(
        CreateESignDocumentFromJobPostCommand request,
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

        var (document, created) = await JobPostESignRenderer.EnsureDocumentAsync(
            _context,
            jobPost,
            _dateTimeService.UtcNow,
            cancellationToken);

        if (created)
        {
            ESignDocumentRevision.Advance(document, _dateTimeService.UtcNow);
            await ESignDocumentRevision.EnqueueAsync(
                _context,
                document,
                _dateTimeService.UtcNow,
                cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return await ESignDocumentProjection.ToResponseAsync(
            _context,
            document,
            request.UserId,
            cancellationToken);
    }
}
