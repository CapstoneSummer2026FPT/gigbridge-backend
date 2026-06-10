using Application.Common.Interfaces.IService;
using MediatR;

namespace Application.Features.Admin.Notifications.Broadcast;

public class CreateBroadcastCommandHandler : IRequestHandler<CreateBroadcastCommand>
{
    private readonly INotificationService _notificationService;

    public CreateBroadcastCommandHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task Handle(CreateBroadcastCommand request, CancellationToken cancellationToken)
    {
        await _notificationService.CreateBroadcastNotificationAsync(
            request.Target,
            (Domain.Enums.NotificationType)request.Type,
            request.Title,
            request.Content,
            request.ReferenceId,
            request.ReferenceType,
            request.TargetUserId,
            request.SendEmail,
            request.CreatedByAdminId,
            request.ExpiresAt,
            cancellationToken);
    }
}
