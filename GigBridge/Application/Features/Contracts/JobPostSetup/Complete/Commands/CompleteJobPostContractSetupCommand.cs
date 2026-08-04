using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.Internal;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.JobPostSetup.Complete.Commands;

public record CompleteJobPostContractSetupCommand(Guid ContractId, Guid UserId) : IRequest<bool>;

public sealed class CompleteJobPostContractSetupCommandHandler
    : IRequestHandler<CompleteJobPostContractSetupCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public CompleteJobPostContractSetupCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<bool> Handle(
        CompleteJobPostContractSetupCommand command,
        CancellationToken cancellationToken)
    {
        var contract = await _context.Set<Contract>()
            .Include(c => c.Milestones)
            .FirstOrDefaultAsync(c => c.ContractsId == command.ContractId, cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException("Contract does not exist.");
        }

        // Verify contract is owned by the client (UserId)
        await ContractParticipantGuard.EnsureClientAsync(_context, contract, command.UserId, cancellationToken);

        var jobPost = await _context.Set<JobPost>()
            .FirstOrDefaultAsync(jp => jp.JobPostsId == contract.JobPostsId, cancellationToken);

        if (jobPost is null)
        {
            throw new NotFoundException("Job post associated with this contract does not exist.");
        }

        await JobPostSetupPublishGuard.EnsureCanPublishAsync(
            _context,
            jobPost,
            contract,
            cancellationToken);

        if (jobPost.Status == JobPostSetupPublishGuard.OpenStatus)
        {
            return true;
        }

        var now = _dateTimeService.UtcNow;

        jobPost.Status = JobPostSetupPublishGuard.OpenStatus;
        jobPost.UpdatedAt = now;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
