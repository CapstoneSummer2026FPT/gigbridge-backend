using Application.Features.ESign.Common.DTOs;
using MediatR;

namespace Application.Features.ESign.Documents.GetById.Queries;

public sealed record GetESignDocumentQuery(
    Guid DocumentId,
    Guid UserId) : IRequest<ESignDocumentResponse>;
