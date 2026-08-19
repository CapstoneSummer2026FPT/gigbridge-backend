namespace Application.Common.Options;

/// <summary>
/// Staged-rollout toggle for Plan B (LISTEN/NOTIFY instead of fixed-timer polling for
/// <c>DeliveryOutboxService</c> and <c>GoogleMeetProvisioningWorker</c>). The dedicated
/// <c>PostgresWorkSignalListener</c> connection always runs regardless of this flag — it's cheap,
/// safe to soak-test in production before anything depends on it. This flag controls only whether
/// workers actually wait on the signal (and whether inserts/re-arms publish one) instead of the
/// original fixed-interval/exponential-backoff polling. Flipping it off is a config change +
/// restart, with zero behavior difference from before Plan B shipped.
/// </summary>
public sealed class WorkSignalOptions
{
    public const string SectionName = "WorkSignal";

    public bool Enabled { get; set; }
}
