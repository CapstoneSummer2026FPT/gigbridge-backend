using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Interfaces.Time;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Receipts.Common.DTOs;
using Application.Features.Receipts.Common.Internal;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Receipts.Prepare.Commands;

public sealed class PrepareProjectReceiptsCommandHandler
    : IRequestHandler<PrepareProjectReceiptsCommand, ProjectReceiptSummaryResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;

    public PrepareProjectReceiptsCommandHandler(IApplicationDbContext context, IDateTimeService clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<ProjectReceiptSummaryResponse> Handle(
        PrepareProjectReceiptsCommand request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        await transaction.AcquireTransactionLockAsync(
            ContractEscrowLock.ForContract(request.ContractId), cancellationToken);
        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(item => item.ContractsId == request.ContractId, cancellationToken)
            ?? throw new NotFoundException("Contract does not exist.");
        var clientUserId = await _context.Set<ClientProfile>()
            .Where(item => item.ClientProfilesId == contract.ClientProfilesId)
            .Select(item => item.UserId)
            .SingleAsync(cancellationToken);
        var freelancerUserId = contract.FreelancerProfilesId.HasValue
            ? await _context.Set<FreelancerProfile>()
                .Where(item => item.FreelancerProfilesId == contract.FreelancerProfilesId.Value)
                .Select(item => (Guid?)item.UserId)
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        var isClient = clientUserId == request.UserId;
        var isFreelancer = freelancerUserId == request.UserId;
        if (!isClient && !isFreelancer)
        {
            throw new ForbiddenAccessException("Only contract participants can prepare receipts.");
        }

        var receipts = await ProjectReceiptWorkflow.EnsurePairAsync(
            _context, request.ContractId, _clock.UtcNow, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var ownReceipt = receipts.Single(item => item.OwnerUserId == request.UserId);
        ownReceipt.Contract = contract;
        return ProjectReceiptResponseMapper.ToSummary(ownReceipt);
    }
}
