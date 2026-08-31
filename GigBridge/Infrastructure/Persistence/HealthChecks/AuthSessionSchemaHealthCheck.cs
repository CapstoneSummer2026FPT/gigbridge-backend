using Application.Common.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Infrastructure.Persistence.HealthChecks;

internal sealed class AuthSessionSchemaHealthCheck : IHealthCheck
{
    private readonly GigbridgeDbContext _context;
    private readonly AuthSessionOptions _options;

    public AuthSessionSchemaHealthCheck(
        GigbridgeDbContext context,
        IOptions<AuthSessionOptions> options)
    {
        _context = context;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return HealthCheckResult.Healthy("Auth sessions are disabled.");
        }

        try
        {
            _ = await _context.AuthSessions
                .AsNoTracking()
                .Select(session => session.Id)
                .Take(1)
                .ToListAsync(cancellationToken);
            return HealthCheckResult.Healthy("AuthSessions schema is available.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "AuthSessions is enabled but its database schema is unavailable.",
                exception);
        }
    }
}
