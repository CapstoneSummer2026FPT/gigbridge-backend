using Application.Features.Contracts.ProductHandoffs.Common.DTOs;
using Domain.Entities;

namespace Application.Features.Contracts.ProductHandoffs.Common;

internal static class ContractProductHandoffMapper
{
    public static ContractProductHandoffResponse ToResponse(ContractProductHandoff handoff)
    {
        return new ContractProductHandoffResponse(
            handoff.ContractProductHandoffId,
            handoff.ContractsId,
            handoff.SubmittedByUserId,
            handoff.SourceType,
            handoff.FileName,
            handoff.FileUrl,
            handoff.MimeType,
            handoff.FileSizeBytes,
            handoff.ExternalUrl,
            handoff.Note,
            handoff.Version,
            handoff.IsCurrent,
            handoff.ReceivedByUserId,
            handoff.ReceivedAt,
            handoff.CreatedAt);
    }
}
