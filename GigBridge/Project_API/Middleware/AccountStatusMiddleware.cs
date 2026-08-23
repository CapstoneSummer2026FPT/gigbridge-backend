using System.Security.Claims;
using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Caching;
using Application.Common.InternalServices.Accounts.Models;
using Application.Common.InternalServices.Accounts.Services;
using Application.Common.Interfaces.Time;
using Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace Project_API.Middleware;

public class AccountStatusMiddleware
{
    private readonly RequestDelegate _next;

    public AccountStatusMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IApplicationDbContext dbContext,
        IDateTimeService dateTimeService,
        ICacheService cache,
        ILogger<AccountStatusMiddleware> logger)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // SignalR negotiation only selects a transport. The authenticated
        // transport request immediately following it performs the account
        // status check, so querying the database here would duplicate that
        // remote lookup on every hub connection.
        if (IsSignalRNegotiateRequest(context.Request))
        {
            await _next(context);
            return;
        }

        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                          context.User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            await _next(context);
            return;
        }

        var cacheKey = AccountAccessCache.Key(userId);
        AccountAccessState? user = null;
        try
        {
            user = await cache.GetAsync<AccountAccessState>(cacheKey, context.RequestAborted);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Account access cache read failed for {UserId}; using PostgreSQL fallback.", userId);
        }
        if (user is null)
        {
            user = await dbContext.Set<Domain.Entities.User>()
                .AsNoTracking()
                .TagWith("Account.AccessStatus")
                .Where(existingUser => existingUser.UserId == userId)
                .Select(existingUser => new AccountAccessState(
                    true,
                    existingUser.IsActive,
                    existingUser.AccountStatus,
                    existingUser.SuspendedUntil))
                .FirstOrDefaultAsync(context.RequestAborted)
                ?? new AccountAccessState(false, false, 0, null);
            await TryCacheAsync(cache, cacheKey, user, userId, logger, context.RequestAborted);
        }

        if (!user.Exists)
        {
            await WriteUnauthorizedAsync(context, "Account does not exist.");
            return;
        }

        if (user.AccountStatus == (int)Domain.Enums.Accounts.AccountStatus.Suspended &&
            user.SuspendedUntil.HasValue && user.SuspendedUntil.Value <= dateTimeService.UtcNow)
        {
            await dbContext.Set<Domain.Entities.User>()
                .Where(existingUser => existingUser.UserId == userId &&
                    existingUser.AccountStatus == (int)Domain.Enums.Accounts.AccountStatus.Suspended &&
                    existingUser.SuspendedUntil <= dateTimeService.UtcNow)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(existingUser => existingUser.AccountStatus,
                        (int)Domain.Enums.Accounts.AccountStatus.Active)
                    .SetProperty(existingUser => existingUser.SuspendedAt, (DateTime?)null)
                    .SetProperty(existingUser => existingUser.SuspendedUntil, (DateTime?)null)
                    .SetProperty(existingUser => existingUser.SuspensionReason, (string?)null),
                    context.RequestAborted);
            user = user with
            {
                AccountStatus = (int)Domain.Enums.Accounts.AccountStatus.Active,
                SuspendedUntil = null
            };
            await TryCacheAsync(cache, cacheKey, user, userId, logger, context.RequestAborted);
        }

        if (!user.IsActive || user.AccountStatus == (int)Domain.Enums.Accounts.AccountStatus.Banned)
        {
            await WriteUnauthorizedAsync(context, "Your account has been permanently banned.");
            return;
        }

        if (user.AccountStatus == (int)Domain.Enums.Accounts.AccountStatus.Suspended &&
            user.SuspendedUntil.HasValue && user.SuspendedUntil.Value > dateTimeService.UtcNow)
        {
            await WriteUnauthorizedAsync(context, $"Your account is suspended until {user.SuspendedUntil.Value:O}");
            return;
        }

        await _next(context);
    }

    private static async Task TryCacheAsync(
        ICacheService cache,
        string cacheKey,
        AccountAccessState state,
        Guid userId,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await cache.SetAsync(cacheKey, state, AccountAccessCache.Duration, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Account access cache write failed for {UserId}.", userId);
        }
    }

    private static bool IsSignalRNegotiateRequest(HttpRequest request)
    {
        return HttpMethods.IsPost(request.Method) &&
               request.Path.StartsWithSegments("/hubs") &&
               request.Path.Value?.EndsWith("/negotiate", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static Task WriteUnauthorizedAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";

        var response = ApiResponse<object>.Error(StatusCodes.Status401Unauthorized, message);
        return context.Response.WriteAsync(JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
