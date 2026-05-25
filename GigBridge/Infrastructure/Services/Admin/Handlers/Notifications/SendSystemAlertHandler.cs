using Application.DTOs.Admin;
using Application.Features.Admin.Notifications.SendSystemAlert;
using Infrastructure.Services.Admin.Interfaces;
using MediatR;

namespace Infrastructure.Services.Admin.Handlers.Notifications;

public sealed class SendSystemAlertHandler : IRequestHandler<SendSystemAlertCommand, SystemAlertResultDto>
{
    private readonly IAdminNotificationService _notificationService;

    public SendSystemAlertHandler(IAdminNotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public Task<SystemAlertResultDto> Handle(SendSystemAlertCommand request, CancellationToken cancellationToken) =>
        _notificationService.SendSystemAlertAsync(request.Request, request.Actor, cancellationToken);
}
