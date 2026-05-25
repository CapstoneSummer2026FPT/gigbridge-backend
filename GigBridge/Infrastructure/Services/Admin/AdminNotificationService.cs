using Application.Common.Interfaces;
using Application.DTOs.Admin;
using Domain.Entities;
using Infrastructure.Services.Admin.Interfaces;

namespace Infrastructure.Services.Admin;

public sealed class AdminNotificationService : AdminServiceBase, IAdminNotificationService
{
    public AdminNotificationService(IApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<SystemAlertResultDto> SendSystemAlertAsync(SystemAlertRequestDto request, AdminActorDto actor, CancellationToken cancellationToken)
    {
        var userIds = request.UserIds.Distinct().ToArray();
        if (userIds.Length == 0)
        {
            throw Invalid("userIds", "At least one recipient identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw Invalid("title", "A notification title is required.");
        }

        foreach (var userId in userIds)
        {
            DbContext.Set<Notification>().Add(new Notification
            {
                NotificationsId = Guid.NewGuid(),
                UserId = userId,
                Type = 10,
                Title = request.Title.Trim(),
                Content = request.Content,
                ReferenceId = request.ReferenceId,
                ReferenceType = request.ReferenceType,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        AddAudit(actor, "SystemAlertSent", request.ReferenceId, request.ReferenceType ?? "Notification", null,
            new { UserIds = userIds, Title = request.Title.Trim(), request.ReferenceId, request.ReferenceType });
        await DbContext.SaveChangesAsync(cancellationToken);
        return new SystemAlertResultDto { RecipientCount = userIds.Length };
    }
}
