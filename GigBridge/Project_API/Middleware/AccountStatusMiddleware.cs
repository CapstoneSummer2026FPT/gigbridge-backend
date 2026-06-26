using System.Security.Claims;
using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
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
            .AsNoTracking()
            .Where(existingUser => existingUser.UserId == userId)
            .Select(existingUser => new
            {
                existingUser.IsActive,
                existingUser.SuspendedUntil
            })
            .FirstOrDefaultAsync(context.RequestAborted);

        if (user is null || !user.IsActive)
        {
            await WriteUnauthorizedAsync(context, "Your account has been suspended by the administrator");
            return;
        }

        if (user.SuspendedUntil.HasValue && user.SuspendedUntil.Value > dateTimeService.UtcNow)
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
