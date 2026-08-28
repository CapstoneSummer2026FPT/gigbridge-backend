namespace Application.Features.Proposals.Common;

internal static class ProposalLock
{
    private const long Namespace = 0x50524F504F53414C;

    public static long ForProposal(Guid proposalId) =>
        BitConverter.ToInt64(proposalId.ToByteArray(), 0) ^ Namespace;
}
