using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces.IService;
using Domain.Enums;

namespace Test_Gigbridge_Backend.TestSupport;

internal sealed class NoopNotificationService : INotificationService
{
    public Task CreateNotificationAsync(
        Guid userId,
        NotificationType type,
        string title,
        string? content = null,
        Guid? referenceId = null,
        string? referenceType = null,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task CreateBroadcastNotificationAsync(
        NotificationTarget target,
        NotificationType type,
        string title,
        string? content = null,
        Guid? referenceId = null,
        string? referenceType = null,
        Guid? targetUserId = null,
        bool sendEmail = false,
        Guid? createdByAdminId = null,
        DateTime? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
