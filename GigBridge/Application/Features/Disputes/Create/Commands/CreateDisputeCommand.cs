using Application.Features.Disputes.Common.DTOs;
using MediatR;

namespace Application.Features.Disputes.Create.Commands;

public sealed record CreateDisputeFile(
    Stream Content,
    string FileName,
    string ContentType,
    long Length);

public sealed record CreateDisputeCommand(
    Guid ContractId,
    Guid UserId,
    string Reason,
    Guid? MilestoneId,
    CreateDisputeFile? Evidence,
    string? EvidenceDescription) : IRequest<DisputeResponse>;
