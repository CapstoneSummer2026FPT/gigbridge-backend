namespace Application.Features.Contracts.Milestones.Client.RespondEarlyStart.DTOs;

public sealed record RespondMilestoneEarlyStartRequest(bool Approve, string? Note);
