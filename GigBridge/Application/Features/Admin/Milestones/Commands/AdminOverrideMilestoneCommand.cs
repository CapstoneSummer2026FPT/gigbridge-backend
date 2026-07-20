using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Wallets.Common;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Milestones.Commands;

public sealed record AdminOverrideMilestoneCommand(
    Guid AdminUserId,
    Guid MilestoneId,
    string Action, // "release" or "refund"
    string? Note) : IRequest<bool>;

public sealed class AdminOverrideMilestoneCommandHandler :
    IRequestHandler<AdminOverrideMilestoneCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public AdminOverrideMilestoneCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(
        AdminOverrideMilestoneCommand request,
        CancellationToken cancellationToken)
    {
        var admin = await _context.Set<User>()
            .FirstOrDefaultAsync(user => user.UserId == request.AdminUserId, cancellationToken);

        if (admin is null || admin.Role != (int)UserRole.Admin)
        {
            throw new ForbiddenAccessException("Only admins can perform milestone overrides.");
        }

        var milestone = await _context.Set<Milestone>()
            .FirstOrDefaultAsync(m => m.MilestonesId == request.MilestoneId, cancellationToken);

        if (milestone is null)
        {
            throw new NotFoundException("Milestone does not exist.");
        }

        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(c => c.ContractsId == milestone.ContractsId, cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException("Contract does not exist.");
        }

        var escrow = await _context.Set<ContractEscrow>()
            .FirstOrDefaultAsync(e => e.ContractsId == contract.ContractsId, cancellationToken);

        if (escrow is null)
        {
            throw new NotFoundException("Contract escrow does not exist.");
        }

        var clientUserId = await _context.Set<ClientProfile>()
            .Where(p => p.ClientProfilesId == contract.ClientProfilesId)
            .Select(p => p.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (clientUserId == Guid.Empty)
        {
            throw new NotFoundException("Contract client profile does not exist.");
        }

        var clientWallet = await _context.Set<UserWallet>()
            .FirstOrDefaultAsync(w => w.UserId == clientUserId, cancellationToken);

        if (clientWallet is null)
        {
            throw new BadRequestException("Client escrow wallet does not exist.");
        }

        var now = DateTime.UtcNow;

        if (request.Action.Equals("release", StringComparison.OrdinalIgnoreCase))
        {
            var releasableVnd = milestone.Amount - milestone.ReleasedAmount;
            if (releasableVnd <= 0)
            {
                throw new BadRequestException("This milestone has no remaining releaseable budget.");
            }

            if (escrow.FundedAmount - escrow.ReleasedAmount < releasableVnd)
            {
                throw new BadRequestException("Escrow balance is insufficient for this release.");
            }

            if (!contract.FreelancerProfilesId.HasValue)
            {
                throw new BadRequestException("Contract does not have a freelancer assigned.");
            }

            var freelancerUserId = await _context.Set<FreelancerProfile>()
                .Where(p => p.FreelancerProfilesId == contract.FreelancerProfilesId.Value)
                .Select(p => p.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            var freelancerWallet = await _context.Set<UserWallet>()
                .FirstOrDefaultAsync(w => w.UserId == freelancerUserId, cancellationToken);

            if (freelancerWallet is null)
            {
                freelancerWallet = new UserWallet
                {
                    UserWalletsId = Guid.NewGuid(),
                    UserId = freelancerUserId,
                    AvailableTokens = 0m,
                    WithdrawableTokens = 0m,
                    HeldTokens = 0m,
                    CreatedAt = now
                };
                _context.Set<UserWallet>().Add(freelancerWallet);
            }

            var transactionCode = $"ESCROW-FORCE-RELEASE-{escrow.ContractEscrowId:N}-{milestone.MilestonesId:N}";
            ContractEscrowWalletWorkflow.Release(
                _context,
                clientWallet,
                freelancerWallet,
                contract.ContractsId,
                escrow.ContractEscrowId,
                milestone.MilestonesId,
                releasableVnd,
                transactionCode,
                "AdminForceRelease",
                request.Note ?? "Force released from escrow by Admin.",
                now);

            milestone.ReleasedAmount += releasableVnd;
            milestone.LastReleasedAt = now;
            milestone.Status = (int)MilestoneStatus.Approved;
            milestone.UpdatedAt = now;

            escrow.ReleasedAmount += releasableVnd;
            escrow.Status = escrow.ReleasedAmount >= escrow.FundedAmount
                ? (int)ContractEscrowStatus.Released
                : (int)ContractEscrowStatus.PartiallyReleased;
            escrow.ReleasedAt = escrow.Status == (int)ContractEscrowStatus.Released ? now : escrow.ReleasedAt;

            contract.UpdatedAt = now;

            _context.Set<EscrowTransaction>().Add(new EscrowTransaction
            {
                EscrowTransactionId = Guid.NewGuid(),
                ContractEscrowId = escrow.ContractEscrowId,
                MilestonesId = milestone.MilestonesId,
                Amount = releasableVnd,
                Type = (int)EscrowTransactionType.ReleaseToFreelancer,
                Status = (int)EscrowTransactionStatus.Succeeded,
                PaymentGateway = "AdminForceRelease",
                GatewayTransactionCode = transactionCode,
                Note = request.Note ?? "Force released by Admin override.",
                CreatedAt = now,
                CompletedAt = now
            });

            await ContractConversationEvents.AddSystemMessageAsync(
                _context,
                contract.ContractsId,
                $"Admin force-released milestone: {milestone.Title}.",
                now,
                cancellationToken);
        }
        else if (request.Action.Equals("refund", StringComparison.OrdinalIgnoreCase))
        {
            var refundableVnd = milestone.Amount - milestone.ReleasedAmount;
            if (refundableVnd <= 0)
            {
                throw new BadRequestException("This milestone has no remaining refundable budget.");
            }

            if (escrow.FundedAmount - escrow.ReleasedAmount < refundableVnd)
            {
                throw new BadRequestException("Escrow balance is insufficient for this refund.");
            }

            var transactionCode = $"ESCROW-REFUND-{escrow.ContractEscrowId:N}-{milestone.MilestonesId:N}";
            ContractEscrowWalletWorkflow.Refund(
                _context,
                clientWallet,
                contract.ContractsId,
                escrow.ContractEscrowId,
                milestone.MilestonesId,
                refundableVnd,
                transactionCode,
                "AdminRefund",
                request.Note ?? "Refunded from escrow back to client by Admin.",
                now);

            milestone.Status = (int)MilestoneStatus.InProgress;
            milestone.UpdatedAt = now;

            escrow.FundedAmount -= refundableVnd;
            escrow.Status = escrow.ReleasedAmount >= escrow.FundedAmount
                ? (int)ContractEscrowStatus.Released
                : (int)ContractEscrowStatus.PartiallyReleased;

            contract.UpdatedAt = now;

            _context.Set<EscrowTransaction>().Add(new EscrowTransaction
            {
                EscrowTransactionId = Guid.NewGuid(),
                ContractEscrowId = escrow.ContractEscrowId,
                MilestonesId = milestone.MilestonesId,
                Amount = refundableVnd,
                Type = (int)EscrowTransactionType.RefundToClient,
                Status = (int)EscrowTransactionStatus.Succeeded,
                PaymentGateway = "AdminRefund",
                GatewayTransactionCode = transactionCode,
                Note = request.Note ?? "Refunded to client by Admin override.",
                CreatedAt = now,
                CompletedAt = now
            });

            await ContractConversationEvents.AddSystemMessageAsync(
                _context,
                contract.ContractsId,
                $"Admin refunded milestone: {milestone.Title}.",
                now,
                cancellationToken);
        }
        else
        {
            throw new BadRequestException("Invalid override action. Supported actions are 'release' and 'refund'.");
        }

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
