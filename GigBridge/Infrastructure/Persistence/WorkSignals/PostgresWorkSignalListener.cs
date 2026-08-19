using Application.Common.InternalServices.WorkSignals.Models;
using Application.Common.InternalServices.WorkSignals.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure.Persistence.WorkSignals;

/// <summary>
/// Dedicated LISTEN connection for every <see cref="WorkSignalChannels"/> channel. Always runs
/// regardless of <c>WorkSignal:Enabled</c> — this is the "inert soak test" piece of Plan B:
/// mergeable and deployable in every environment to prove connection stability under real
/// production conditions before any worker relies on it (see <see cref="WorkSignalSaveChangesInterceptor"/>
/// and the workers, which are the parts actually gated by the config toggle).
///
/// Reconnects with exponential backoff (capped) on any failure — an idle-reap, a network blip, a
/// Supavisor restart — and re-issues LISTEN for every channel on each new connection, since a
/// fresh connection starts with zero subscriptions.
/// </summary>
public sealed class PostgresWorkSignalListener(
    IConfiguration configuration,
    IServiceProvider serviceProvider,
    ILogger<PostgresWorkSignalListener> logger) : BackgroundService
{
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = BuildConnectionString();
        var backoff = InitialBackoff;

        while (!stoppingToken.IsCancellationRequested)
        {
            NpgsqlConnection? connection = null;
            try
            {
                connection = new NpgsqlConnection(connectionString);
                connection.Notification += OnNotification;
                await connection.OpenAsync(stoppingToken);

                foreach (var channel in WorkSignalChannels.All)
                {
                    await using var command = new NpgsqlCommand($"LISTEN {channel}", connection);
                    await command.ExecuteNonQueryAsync(stoppingToken);
                }

                logger.LogInformation(
                    "Work signal listener connected and subscribed to {ChannelCount} channel(s).",
                    WorkSignalChannels.All.Count);
                backoff = InitialBackoff;

                while (!stoppingToken.IsCancellationRequested)
                {
                    // Blocks until a notification arrives (firing OnNotification synchronously
                    // before returning) or the token is cancelled. Verified against Npgsql 8.0.5
                    // through the Supavisor session pooler in the Phase 0 spike.
                    await connection.WaitAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Work signal listener connection failed; retrying in {DelaySeconds}s.",
                    backoff.TotalSeconds);
            }
            finally
            {
                if (connection is not null)
                {
                    connection.Notification -= OnNotification;
                    await connection.DisposeAsync();
                }
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await Task.Delay(backoff, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, MaxBackoff.Ticks));
        }
    }

    private void OnNotification(object? sender, NpgsqlNotificationEventArgs e)
    {
        try
        {
            serviceProvider.GetKeyedService<WorkSignalGate>(e.Channel)?.Signal();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to relay NOTIFY for channel {Channel}.", e.Channel);
        }
    }

    private string BuildConnectionString()
    {
        var baseConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        return ListenerConnectionStringFactory.Build(baseConnectionString, configuration);
    }
}
