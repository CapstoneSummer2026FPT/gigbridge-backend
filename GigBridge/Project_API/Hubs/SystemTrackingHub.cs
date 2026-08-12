using Domain.Enums.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Project_API.Hubs;

[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class SystemTrackingHub : Hub
{
    public const string SnapshotUpdatedEvent = "SystemTrackingUpdated";
}
