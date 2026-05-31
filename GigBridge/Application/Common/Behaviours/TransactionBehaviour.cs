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
    private readonly ITransactionManager _transactionManager;
    private readonly ILogger<TRequest> _logger;

    public TransactionBehaviour(ITransactionManager transactionManager, ILogger<TRequest> logger)
    {
        _transactionManager = transactionManager;
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

        await using var transaction = await _transactionManager.BeginTransactionAsync(cancellationToken);

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
