namespace Application.Common.InternalServices.WorkSignals.Interfaces;

/// <summary>
/// Sends a Postgres NOTIFY for <paramref name="channel"/> so every app instance's
/// <c>PostgresWorkSignalListener</c> wakes the matching local <see cref="IWorkSignalSource"/>.
/// Used by <c>WorkSignalSaveChangesInterceptor</c> for tracked inserts, and directly by the two
/// call sites that re-arm rows via <c>ExecuteUpdateAsync</c> (which bypasses the change tracker
/// the interceptor watches).
/// </summary>
public interface IWorkSignalPublisher
{
    Task PublishAsync(string channel, CancellationToken cancellationToken);
}
