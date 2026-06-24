namespace Application.Features.Proposals.Common.UpdateProposalStatus.Commands.DTOs;

public class UpdateProposalStatusRequest
{
    /// <summary>
    /// Enum ProposalStatus:
    /// 0=Draft, 1=Pending, 2=Shortlisted, 3=Accepted, 4=Rejected, 5=Withdrawn
    /// </summary>
    public int Status { get; set; }
}
