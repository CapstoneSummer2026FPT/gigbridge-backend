using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Common.Behaviours;

/// <summary>
/// Pipeline behaviour that enforces authentication for requests implementing IRequireAuthentication.
/// Runs before the handler and throws UnauthorizedAccessException if the user is not authenticated.
/// </summary>
public class AuthorizationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<TRequest> _logger;

    public AuthorizationBehaviour(ICurrentUserService currentUserService, ILogger<TRequest> logger)
    {
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is IRequireAuthentication)
        {
            var userId = _currentUserService.UserId;

            if (string.IsNullOrEmpty(userId))
            {
                var requestName = typeof(TRequest).Name;
                _logger.LogWarning("GigBridge Authorization: Unauthenticated request {Name}", requestName);
                throw new UnauthorizedAccessException("Authentication is required to perform this action.");
            }
        }

        if (request is IRequireAdmin && _currentUserService.Role != nameof(UserRole.Admin))
        {
            var requestName = typeof(TRequest).Name;
            _logger.LogWarning("GigBridge Authorization: Forbidden admin request {Name}", requestName);
            throw new ForbiddenAccessException();
        }

        return await next();
    }
}
