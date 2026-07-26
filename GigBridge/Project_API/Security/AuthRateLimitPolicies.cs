using System.Threading.RateLimiting;
using Application.Common.Models;
using Microsoft.AspNetCore.RateLimiting;

namespace Project_API.Security;

public static class AuthRateLimitPolicies
{
    public const string Account = "auth-account";
    public const string Login = "auth-login";
    public const string OtpIssue = "auth-otp-issue";
    public const string OtpVerify = "auth-otp-verify";
    public const string Refresh = "auth-refresh";

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    ApiResponse<object>.Error(
                        StatusCodes.Status429TooManyRequests,
                        "Too many requests. Please try again later."),
                    cancellationToken);
            };

            AddFixedWindow(options, Account, permitLimit: 5, TimeSpan.FromMinutes(5));
            AddFixedWindow(options, Login, permitLimit: 10, TimeSpan.FromMinutes(1));
            AddFixedWindow(options, OtpIssue, permitLimit: 3, TimeSpan.FromMinutes(5));
            AddFixedWindow(options, OtpVerify, permitLimit: 10, TimeSpan.FromMinutes(5));
            AddFixedWindow(options, Refresh, permitLimit: 30, TimeSpan.FromMinutes(1));
        });

        return services;
    }

    private static void AddFixedWindow(
        RateLimiterOptions options,
        string policyName,
        int permitLimit,
        TimeSpan window)
    {
        options.AddPolicy(policyName, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
    }
}
