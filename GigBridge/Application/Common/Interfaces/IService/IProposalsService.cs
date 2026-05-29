using Application.Features.Proposals.SubmitProposal.Commands;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Proposals.Services;

public interface IProposalsService
{
    Task<Guid> SubmitProposalAsync(SubmitProposalCommand command, CancellationToken cancellationToken = default);
}