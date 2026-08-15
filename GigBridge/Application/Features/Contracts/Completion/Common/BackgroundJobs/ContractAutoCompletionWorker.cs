using Application.Common.Interfaces;
using Application.Features.Contracts.Completion.Client.Commands;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Milestones;
using Domain.Enums.Disputes;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Application.Features.Contracts.Completion.Common.BackgroundJobs;

public sealed class ContractAutoCompletionWorker : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);
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
        var candidates = await context.Set<Domain.Entities.Contract>()
            .AsNoTracking()
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
                    dispute.Status != (int)DisputeStatus.Closed))
            .OrderBy(contract => contract.UpdatedAt ?? contract.CreatedAt)
            .Select(contract => new
            {
                contract.ContractsId,
                ClientUserId = contract.ClientProfiles.UserId
            })
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            try
            {
                await mediator.Send(
                    new EndProjectCommand(candidate.ContractsId, candidate.ClientUserId),
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(
                    exception,
                    "Automatic completion failed for contract {ContractId}.",
                    candidate.ContractsId);
            }
        }
    }
}
