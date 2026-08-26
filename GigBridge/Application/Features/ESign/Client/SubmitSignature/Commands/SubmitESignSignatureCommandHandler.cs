using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Media;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.ESign.Services;
using Application.Features.ESign.Common.DTOs;
using Application.Features.ESign.Common.Internal;
using Domain.Entities;
using Domain.Enums.ESign;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ESign.Client.SubmitSignature.Commands;

public sealed class SubmitESignSignatureCommandHandler
    : IRequestHandler<SubmitESignSignatureCommand, ESignSignatureResponse>
{
    private const int OpenJobPostStatus = 1;

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IMediaService _mediaService;

    public SubmitESignSignatureCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IMediaService mediaService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _mediaService = mediaService;
    }

    public async Task<ESignSignatureResponse> Handle(
        SubmitESignSignatureCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        var document = await ESignAccessGuard.GetDocumentAsync(
            _context,
            request.DocumentId,
            cancellationToken);

        if (document.ContractsId.HasValue)
        {
            throw new BadRequestException("Use the contract signing endpoint to sign contract documents.");
        }

        var jobPost = await ESignAccessGuard.GetJobPostAsync(
            _context,
            document.JobPostsId,
            cancellationToken);

        await ESignAccessGuard.EnsureOwningClientAsync(
            _context,
            jobPost,
            command.UserId,
            cancellationToken);

        var existingSignature = await _context.Set<EsignSignature>()
            .FirstOrDefaultAsync(
                signature =>
                    signature.EsignDocumentsId == document.EsignDocumentsId &&
                    signature.UserId == command.UserId,
                cancellationToken);

        if (existingSignature is not null &&
            existingSignature.Status == (int)ESignSignatureStatus.Signed)
        {
            throw new ConflictException("This user has already signed this e-sign document.");
        }

        if (document.Status == (int)ESignDocumentStatus.FullySigned)
        {
            throw new ConflictException("This e-sign document is already fully signed.");
        }

        var now = _dateTimeService.UtcNow;
        var signatureImageUrl = await SignatureImageStorage.UploadSignatureImageAsync(
            _mediaService,
            request.SignatureImageUrl,
            document.EsignDocumentsId,
            command.UserId,
            cancellationToken);

        var signature = existingSignature ?? new EsignSignature
        {
            EsignSignaturesId = Guid.NewGuid(),
            EsignDocumentsId = document.EsignDocumentsId,
            UserId = command.UserId,
            SignerRole = (int)ESignerRole.Client,
            CreatedAt = now
        };

        if (existingSignature is null)
        {
            _context.Set<EsignSignature>().Add(signature);
        }

        signature.SignatureImageUrl = signatureImageUrl;
        signature.SignatureWidth = request.SignatureWidth;
        signature.SignatureHeight = request.SignatureHeight;
        signature.Status = (int)ESignSignatureStatus.Signed;
        signature.SignedAt = now;
        signature.DeclinedAt = null;
        signature.DeclineReason = null;
        signature.IpAddress = command.IpAddress;
        signature.UserAgent = command.UserAgent;

        document.Status = (int)ESignDocumentStatus.FullySigned;
        document.FinalizedAt = now;
        ESignDocumentRevision.Advance(document, now);
        await ESignDocumentRevision.EnqueueAsync(
            _context,
            document,
            now,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return ESignDocumentProjection.ToSignatureResponse(signature, command.UserId);
    }
}
