using Application.Features.ESign.Common.DTOs;
using Application.Features.ESign.Signatures.Submit.DTOs;
using MediatR;

namespace Application.Features.ESign.Signatures.Submit.Commands;

public sealed record SubmitESignSignatureCommand(
    Guid UserId,
    SubmitESignSignatureRequest Request,
    string? IpAddress,
    string? UserAgent) : IRequest<ESignSignatureResponse>;
