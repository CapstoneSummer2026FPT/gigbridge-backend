namespace Application.Common.InternalServices.WorkSignals.Models;

/// <summary>
/// Postgres NOTIFY channel names shared between the publishing side (EF interceptor, explicit
/// bulk-update call sites) and the listening side (<c>PostgresWorkSignalListener</c> in
/// Infrastructure). Also used as DI keys for the per-channel <c>WorkSignalGate</c>.
/// </summary>
public static class WorkSignalChannels
{
    public const string DeliveryOutbox = "gigbridge_delivery_outbox";
    public const string GoogleMeetProvisioning = "gigbridge_google_meet_provisioning";
    public const string ProjectReceipt = "gigbridge_project_receipt";

    public static readonly IReadOnlyList<string> All = [DeliveryOutbox, GoogleMeetProvisioning, ProjectReceipt];
}
