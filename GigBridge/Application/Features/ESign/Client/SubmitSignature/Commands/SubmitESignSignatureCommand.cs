using Application.Features.ESign.Common.DTOs;
using Application.Features.ESign.Client.SubmitSignature.DTOs;
using MediatR;

namespace Application.Features.ESign.Client.SubmitSignature.Commands;

public sealed record SubmitESignSignatureCommand(
    Guid UserId,
    SubmitESignSignatureRequest Request,
    string? IpAddress,
    string? UserAgent) : IRequest<ESignSignatureResponse>;
