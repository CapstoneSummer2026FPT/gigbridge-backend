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
    public const string DiscoveryAnalytics = "discovery-analytics";
    public const string PromotionTelemetry = "promotion-telemetry";

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

            AddFixedWindow(options, Account, permitLimit: 50, TimeSpan.FromMinutes(5));
            AddFixedWindow(options, Login, permitLimit: 50, TimeSpan.FromMinutes(1));
            AddFixedWindow(options, OtpIssue, permitLimit: 50, TimeSpan.FromMinutes(5));
            AddFixedWindow(options, OtpVerify, permitLimit: 50, TimeSpan.FromMinutes(5));
            AddFixedWindow(options, Refresh, permitLimit: 150, TimeSpan.FromMinutes(1));
            AddFixedWindow(options, DiscoveryAnalytics, permitLimit: 300, TimeSpan.FromMinutes(1));
            AddFixedWindow(options, PromotionTelemetry, permitLimit: 300, TimeSpan.FromMinutes(1));
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
                GetClientIp(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
    }

    private static string GetClientIp(HttpContext httpContext)
    {
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var clientIp = forwardedFor.Split(',')[0].Trim();
            if (!string.IsNullOrWhiteSpace(clientIp))
                return clientIp;
        }

        var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(realIp))
        {
            return realIp.Trim();
        }

        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrWhiteSpace(remoteIp) && remoteIp != "127.0.0.1" && remoteIp != "::1")
        {
            return remoteIp;
        }

        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        return $"fallback_{remoteIp ?? "unknown"}_{userAgent.GetHashCode()}";
    }
}
