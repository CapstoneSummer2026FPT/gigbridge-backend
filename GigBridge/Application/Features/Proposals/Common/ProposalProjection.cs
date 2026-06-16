using Application.Features.Proposals.Common.DTOs;
using Domain.Entities;

namespace Application.Features.Proposals.Common;

internal static class ProposalProjection
{
    public static List<ProposalDto> ToDtos(IEnumerable<Proposal> proposals)
    {
        return proposals.Select(ToDto).ToList();
    }

    private static ProposalDto ToDto(Proposal proposal)
    {
        return new ProposalDto
        {
            ProposalsId = proposal.ProposalsId,
            JobPostsId = proposal.JobPostsId,
            JobTitle = proposal.JobPosts?.Title ?? string.Empty,
            FreelancerProfilesId = proposal.FreelancerProfilesId,
            FreelancerName = proposal.FreelancerProfiles?.User?.FullName ?? string.Empty,
            CoverLetter = proposal.CoverLetter ?? string.Empty,
            ProposedBudget = proposal.ProposedBudget ?? 0m,
            ProposedDuration = proposal.ProposedDuration ?? string.Empty,
            Status = proposal.Status,
            SubmittedAt = proposal.SubmittedAt ?? DateTime.MinValue,
            ReviewedAt = proposal.UpdatedAt
        };
    }
}
