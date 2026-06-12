using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Details.Client.Submit.Commands;

public sealed class SubmitContractDetailsCommandHandler :
    IRequestHandler<SubmitContractDetailsCommand, ContractWorkflowResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public SubmitContractDetailsCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<ContractWorkflowResponse> Handle(
        SubmitContractDetailsCommand command,
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
            throw new BadRequestException("Only pending contract details can be submitted.");
        }

        await ContractParticipantGuard.EnsureClientAsync(_context, contract, command.UserId, cancellationToken);

        var milestones = await _context.Set<Milestone>()
            .Where(milestone => milestone.ContractsId == contract.ContractsId)
            .ToListAsync(cancellationToken);

        ContractDetailsValidator.ValidateTerms(contract);
        ContractDetailsValidator.ValidateMilestones(contract, milestones);

        var now = _dateTimeService.UtcNow;
        contract.Status = (int)ContractStatus.PendingContractConfirmation;
        contract.UpdatedAt = now;

        await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            "Contract details submitted for freelancer confirmation.",
            now,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return new ContractWorkflowResponse(contract.ContractsId, contract.Status, null, null);
    }
}
