using Application.Features.ESign.Common.DTOs;
using MediatR;

namespace Application.Features.ESign.Common.GeneratePdf.Commands;

public sealed record GenerateESignPdfCommand(Guid DocumentId, Guid UserId)
    : IRequest<ESignPdfArtifactResponse>;
