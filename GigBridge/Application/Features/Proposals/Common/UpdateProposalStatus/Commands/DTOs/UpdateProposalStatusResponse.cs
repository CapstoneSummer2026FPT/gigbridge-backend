namespace Application.Features.Proposals.Common.UpdateProposalStatus.Commands.DTOs;

public record UpdateProposalStatusResponse(
    bool Success,
    int Status,
    CheatingPenaltyResultDto? CheatingPenalty);
