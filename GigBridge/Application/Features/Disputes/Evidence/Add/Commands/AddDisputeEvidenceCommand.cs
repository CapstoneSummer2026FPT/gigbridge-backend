using Application.Features.Disputes.Common.DTOs;
using Application.Features.Disputes.Common.Internal;
using MediatR;

namespace Application.Features.Disputes.Evidence.Add.Commands;

public sealed record AddDisputeEvidenceCommand(
    Guid ContractId,
    Guid DisputeId,
    Guid UserId,
    IReadOnlyList<DisputeEvidenceFile> Files,
    Guid? RequestEvidenceId = null) : IRequest<IReadOnlyList<DisputeEvidenceResponse>>;
