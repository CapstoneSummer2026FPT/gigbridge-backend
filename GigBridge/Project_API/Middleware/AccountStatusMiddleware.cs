using System.Security.Claims;
using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Services;
using Application.Common.Models;
using Domain.Entities;
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
        IDateTimeService dateTimeService)
    {
        if (context.User.Identity?.IsAuthenticated != true)
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

        var user = await dbContext.Set<User>()
            .Where(existingUser => existingUser.UserId == userId)
            .FirstOrDefaultAsync(context.RequestAborted);

        if (user is null)
        {
            await WriteUnauthorizedAsync(context, "Account does not exist.");
            return;
        }

        if (UserAccountEnforcement.NormalizeExpiredSuspension(user, dateTimeService.UtcNow))
            await dbContext.SaveChangesAsync(context.RequestAborted);

        if (!user.IsActive || user.AccountStatus == (int)Domain.Enums.AccountStatus.Banned)
        {
            await WriteUnauthorizedAsync(context, "Your account has been permanently banned.");
            return;
        }

        if (user.AccountStatus == (int)Domain.Enums.AccountStatus.Suspended &&
            user.SuspendedUntil.HasValue && user.SuspendedUntil.Value > dateTimeService.UtcNow)
        {
            await WriteUnauthorizedAsync(context, $"Your account is suspended until {user.SuspendedUntil.Value:O}");
            return;
        }

        await _next(context);
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
