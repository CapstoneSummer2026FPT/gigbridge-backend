using Application.Common.Interfaces;
using Application.Features.Contracts.Completion.Client.Commands;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Escrow;
using Domain.Enums.Contracts.Milestones;
using Domain.Enums.Disputes;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Application.Common.InternalServices.Contracts.Completion.BackgroundJobs;
public sealed class ContractAutoCompletionWorker : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);
    private const decimal FinancialTolerance = 0.01m;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ContractAutoCompletionWorker> _logger;

    public ContractAutoCompletionWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ContractAutoCompletionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Automatic contract completion batch failed.");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    internal async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var cutoff = DateTime.UtcNow.AddHours(-72);
        var candidates = await CandidateQuery(context, cutoff)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            try
            {
                await mediator.Send(
                    new EndProjectCommand(candidate.ContractId, candidate.ClientUserId),
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(
                    exception,
                    "Automatic completion failed for contract {ContractId}.",
                    candidate.ContractId);
            }
        }
    }

    internal static IQueryable<ContractAutoCompletionCandidate> CandidateQuery(
        IApplicationDbContext context,
        DateTime cutoff) =>
        context.Set<Domain.Entities.Contract>()
            .AsNoTracking()
            .TagWith("Contract.AutoCompletion.Candidates")
            .Where(contract =>
                contract.Status == (int)ContractStatus.Active &&
                contract.Milestones.Any() &&
                !contract.Milestones.Any(milestone =>
                    milestone.Status != (int)MilestoneStatus.Approved &&
                    milestone.Status != (int)MilestoneStatus.Completed ||
                    !milestone.ApprovedAt.HasValue ||
                    milestone.ApprovedAt.Value > cutoff) &&
                !contract.Disputes.Any(dispute =>
                    dispute.Status != (int)DisputeStatus.Resolved &&
                    dispute.Status != (int)DisputeStatus.Closed) &&
                contract.ContractEscrow != null &&
                (contract.ContractEscrow.Status == (int)ContractEscrowStatus.Funded ||
                 contract.ContractEscrow.Status == (int)ContractEscrowStatus.PartiallyReleased ||
                 contract.ContractEscrow.Status == (int)ContractEscrowStatus.Released) &&
                Math.Abs(
                    contract.ContractEscrow.FundedAmount -
                    contract.Milestones.Sum(milestone => milestone.Amount - milestone.RefundedAmount)) <= FinancialTolerance &&
                !contract.Milestones.Any(milestone =>
                    milestone.Amount - milestone.RefundedAmount - milestone.ReleasedAmount < -FinancialTolerance) &&
                Math.Abs(
                    contract.ContractEscrow.FundedAmount - contract.ContractEscrow.ReleasedAmount -
                    contract.Milestones.Sum(milestone =>
                        milestone.Amount - milestone.RefundedAmount - milestone.ReleasedAmount)) <= FinancialTolerance &&
                (contract.ContractEscrow.DepositedTokens + contract.ContractEscrow.EarnedTokens <= FinancialTolerance ||
                 Math.Abs(
                     contract.ContractEscrow.DepositedTokens + contract.ContractEscrow.EarnedTokens -
                     (contract.ContractEscrow.FundedAmount - contract.ContractEscrow.ReleasedAmount)) <= FinancialTolerance) &&
                contract.ClientProfiles.User.UserWallet != null &&
                contract.ClientProfiles.User.UserWallet.HeldTokens + FinancialTolerance >=
                    contract.ContractEscrow.FundedAmount - contract.ContractEscrow.ReleasedAmount)
            .OrderBy(contract => contract.UpdatedAt ?? contract.CreatedAt)
            .Select(contract => new ContractAutoCompletionCandidate(
                contract.ContractsId,
                contract.ClientProfiles.UserId));
}

internal sealed record ContractAutoCompletionCandidate(Guid ContractId, Guid ClientUserId);
