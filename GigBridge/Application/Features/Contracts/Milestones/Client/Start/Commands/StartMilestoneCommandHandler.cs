using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.Common.Internal;

using MediatR;

namespace Application.Features.Contracts.Milestones.Client.Start.Commands;

public sealed class StartMilestoneCommandHandler :
    IRequestHandler<StartMilestoneCommand, ContractMilestoneResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public StartMilestoneCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<ContractMilestoneResponse> Handle(
        StartMilestoneCommand command,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        throw new BadRequestException("Manual milestone start is deprecated. Milestones start automatically after funding or approval, or through an approved early-start request.");
    }
}
