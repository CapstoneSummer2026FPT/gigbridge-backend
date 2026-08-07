using Application.Features.ESign.Common.DTOs;
using MediatR;

namespace Application.Features.ESign.Common.SavePdf.Commands;

public sealed record SaveESignPdfCommand(
    Guid DocumentId,
    Guid UserId,
    byte[] Content,
    string FileName,
    int SignatureCount) : IRequest<ESignPdfArtifactResponse>;
