using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Details.Client.Update.Commands;

public sealed class UpdateContractDetailsCommandHandler :
    IRequestHandler<UpdateContractDetailsCommand, ContractWorkflowResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public UpdateContractDetailsCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<ContractWorkflowResponse> Handle(
        UpdateContractDetailsCommand command,
        CancellationToken cancellationToken)
    {
        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(contract => contract.ContractsId == command.ContractId, cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException("Contract does not exist.");
        }

        if (contract.Status != (int)ContractStatus.PendingContractDetails)
        {
            throw new BadRequestException("Contract details can only be edited while pending details.");
        }

        await ContractParticipantGuard.EnsureClientAsync(_context, contract, command.UserId, cancellationToken);

        var now = _dateTimeService.UtcNow;
        contract.ScopeOfWork = command.Request.ScopeOfWork;
        contract.PaymentTerms = command.Request.PaymentTerms;
        contract.IntellectualPropertyTerms = command.Request.IntellectualPropertyTerms;
        contract.ConfidentialityTerms = command.Request.ConfidentialityTerms;
        contract.CancellationTerms = command.Request.CancellationTerms;
        contract.DisputeTerms = command.Request.DisputeTerms;
        contract.UpdatedAt = now;

        ContractDetailsValidator.ValidateTerms(contract);

        var newMilestones = command.Request.Milestones
            .Select((milestone, index) => new Milestone
            {
                MilestonesId = milestone.MilestoneId ?? Guid.NewGuid(),
                ContractsId = contract.ContractsId,
                Title = milestone.Title,
                Amount = milestone.Amount,
                DueDate = milestone.DueDate,
                SortOrder = milestone.SortOrder ?? index,
                Status = (int)MilestoneStatus.Pending,
                CreatedAt = now
            })
            .ToList();

        ContractDetailsValidator.ValidateMilestones(contract, newMilestones);

        var existingMilestones = await _context.Set<Milestone>()
            .Where(milestone => milestone.ContractsId == contract.ContractsId)
            .ToListAsync(cancellationToken);

        _context.Set<Milestone>().RemoveRange(existingMilestones);
        foreach (var milestone in newMilestones)
        {
            _context.Set<Milestone>().Add(milestone);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new ContractWorkflowResponse(contract.ContractsId, contract.Status, null, null);
    }
}
