using MediatR;
using Microsoft.Extensions.Logging;
using Application.Common.Exceptions;

namespace Application.Common.Behaviours;

public class UnhandledExceptionBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private readonly ILogger<TRequest> _logger;
    public UnhandledExceptionBehaviour(ILogger<TRequest> logger)
    {
        _logger = logger;
    }
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(
                "GigBridge Request: Request {Name} was canceled.",
                typeof(TRequest).Name);
            throw;
        }
        catch (Exception ex)
        {
            var requestName = typeof(TRequest).Name;

            if (ex is not ValidationException &&
                ex is not ConflictException &&
                ex is not BadRequestException &&
                ex is not UnauthorizedAccessException &&
                ex is not NotFoundException &&
                ex is not ExternalServiceException &&
                ex is not ForbiddenAccessException)
            {
                _logger.LogError(ex, "GigBridge Request: Unhandled Exception for Request {Name}", requestName);
            }
            else
            {
                _logger.LogWarning(ex, "GigBridge Request: Expected Application Exception for Request {Name}", requestName);
            }
            throw;
        }
    }
}
