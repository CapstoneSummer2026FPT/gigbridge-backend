using Application.Features.Proposals.DTOs;
using Application.Features.Proposals.GetAllProposals.Queries;
using Application.Features.Proposals.GetMyProposals.Queries;
using Application.Features.Proposals.GetProposalsByJobPost.Queries;
using Application.Features.Proposals.SubmitProposal.Commands;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Proposals.Services;

public interface IProposalsService
{
    Task<Guid> SubmitProposalAsync(SubmitProposalCommand command, CancellationToken cancellationToken = default);

    Task<IEnumerable<ProposalDto>> GetMyProposalsAsync(GetMyProposalsQuery request, CancellationToken cancellationToken = default);

    Task<IEnumerable<ProposalDto>> GetProposalsByJobPostAsync(GetProposalsByJobPostQuery request, CancellationToken cancellationToken = default);

    Task<IEnumerable<ProposalDto>> GetAllProposalsAsync(GetAllProposalsQuery request, CancellationToken cancellationToken = default);
}