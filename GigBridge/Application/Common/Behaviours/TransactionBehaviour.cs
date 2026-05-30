using Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Common.Behaviours;

/// <summary>
/// Pipeline behaviour that wraps Command handlers in a database transaction.
/// Only applies to requests whose type name ends with "Command" (convention-based).
/// Queries are skipped since they should not modify data.
/// On success the transaction is committed; on exception it is rolled back automatically.
/// </summary>
public class TransactionBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<TRequest> _logger;

    public TransactionBehaviour(IApplicationDbContext dbContext, ILogger<TRequest> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        // Only wrap Commands in a transaction, skip Queries
        if (!requestName.EndsWith("Command"))
        {
            return await next();
        }

        // EF Core DbContext tracks changes and SaveChangesAsync is called by the handler/service.
        // We wrap the entire handler execution in a transaction so that multiple SaveChanges calls
        // within a single command are atomic.
        await using var transaction = await (_dbContext as Microsoft.EntityFrameworkCore.DbContext)!
            .Database.BeginTransactionAsync(cancellationToken);

        try
        {
            _logger.LogDebug("GigBridge Transaction: Begin for {Name}", requestName);

            var response = await next();

            await transaction.CommitAsync(cancellationToken);

            _logger.LogDebug("GigBridge Transaction: Committed for {Name}", requestName);

            return response;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "GigBridge Transaction: Rolled back for {Name}", requestName);
            throw;
        }
    }
}
