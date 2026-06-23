using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.Internal;
using Domain.Entities;
using Domain.Enums;
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

        if (jobPost.Status != 0) // Draft is 0
        {
            throw new BadRequestException("Job post must be in Draft status to complete setup.");
        }

        // Verify JobPost E-sign document is FullySigned
        var isEsignFullySigned = await _context.Set<EsignDocument>()
            .AnyAsync(
                doc => doc.JobPostsId == jobPost.JobPostsId &&
                       doc.Status == (int)ESignDocumentStatus.FullySigned,
                cancellationToken);

        if (!isEsignFullySigned)
        {
            throw new BadRequestException("Job post e-sign document is not fully signed.");
        }

        // Validate Milestones
        if (contract.Milestones.Count == 0)
        {
            throw new BadRequestException("At least one milestone is required.");
        }

        foreach (var milestone in contract.Milestones)
        {
            if (string.IsNullOrWhiteSpace(milestone.Title))
            {
                throw new BadRequestException("Milestone title cannot be empty.");
            }

            if (milestone.Amount <= 0)
            {
                throw new BadRequestException("Milestone amount must be positive.");
            }
        }

        var milestonesSum = contract.Milestones.Sum(m => m.Amount);
        if (milestonesSum != contract.TotalBudget)
        {
            throw new BadRequestException($"Total milestone budget ({milestonesSum}) must match contract budget ({contract.TotalBudget}).");
        }

        var now = _dateTimeService.UtcNow;

        // Publish the job post by setting Status = Open (1)
        jobPost.Status = 1;
        jobPost.UpdatedAt = now;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
