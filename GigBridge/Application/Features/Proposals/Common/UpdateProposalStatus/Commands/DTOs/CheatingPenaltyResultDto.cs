namespace Application.Features.Proposals.Common.UpdateProposalStatus.Commands.DTOs;

public record CheatingPenaltyResultDto(
    bool Applied,
    bool IsNewViolation,
    Guid ViolationId,
    int ViolationNumber,
    int EloDelta,
    int Action,
    DateTime? SuspendedUntil,
    string Message);
