using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Infrastructure.Persistence.HealthChecks;

internal sealed class AuthSessionSchemaHealthCheck : IHealthCheck
{
    private readonly GigbridgeDbContext _context;

    public AuthSessionSchemaHealthCheck(GigbridgeDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
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
                "AuthSessions database schema is unavailable.",
                exception);
        }
    }
}
