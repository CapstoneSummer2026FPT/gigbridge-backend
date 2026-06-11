namespace Application.Features.Proposals.Common.UpdateProposalStatus.Commands.DTOs;

public class UpdateProposalStatusRequest
{
    /// <summary>
    /// Enum ProposalStatus:
    /// 0=Pending, 1=Shortlisted, 2=Accepted, 3=Rejected, 4=Withdrawn
    /// </summary>
    public int Status { get; set; }
}