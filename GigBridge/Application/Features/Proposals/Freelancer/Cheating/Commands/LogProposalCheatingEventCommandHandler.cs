using Application.Common.Interfaces.IService;
using Application.Features.Proposals.Freelancer.Cheating.DTOs;
using MediatR;

namespace Application.Features.Proposals.Freelancer.Cheating.Commands;

public class LogProposalCheatingEventCommandHandler
    : IRequestHandler<LogProposalCheatingEventCommand, CheatingEventLogResponse>
{
    private readonly IProposalCheatingService _proposalCheatingService;

    public LogProposalCheatingEventCommandHandler(IProposalCheatingService proposalCheatingService)
    {
        _proposalCheatingService = proposalCheatingService;
    }

    public Task<CheatingEventLogResponse> Handle(
        LogProposalCheatingEventCommand command,
        CancellationToken cancellationToken)
    {
        return _proposalCheatingService.LogEventAsync(
            command.ProposalId,
            command.FreelancerUserId,
            command.Request,
            command.IpAddress,
            command.UserAgent,
            cancellationToken);
    }
}
