using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Infrastructure.Persistence.WorkSignals;

/// <summary>
/// Builds the connection string for the dedicated LISTEN connection. Deliberately not drawn from
/// the small EF pool (<c>Pooling=false</c>) and tagged with its own <c>ApplicationName</c> so it's
/// identifiable in <c>pg_stat_activity</c> separately from request/worker traffic.
///
/// <c>Keepalive=30</c> is defense-in-depth, not a measured necessity: the Phase 0 spike found no
/// idle reap from Supavisor at all across a 20-minute unkept-alive window (see the egress
/// remediation plan doc's Current Status section), but a modest keepalive protects against a
/// different failure mode — NAT/load-balancer idle reaping on the network path — that the spike
/// didn't isolate.
/// </summary>
internal static class ListenerConnectionStringFactory
{
    private const int KeepaliveSeconds = 30;

    public static string Build(string baseConnectionString, IConfiguration configuration)
    {
        var environmentName = configuration["ASPNETCORE_ENVIRONMENT"] ??
                               configuration["DOTNET_ENVIRONMENT"] ??
                               "Unknown";

        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Pooling = false,
            KeepAlive = KeepaliveSeconds,
            ApplicationName = $"GigBridge-{environmentName}-WorkSignalListener"
        };

        return builder.ConnectionString;
    }
}
