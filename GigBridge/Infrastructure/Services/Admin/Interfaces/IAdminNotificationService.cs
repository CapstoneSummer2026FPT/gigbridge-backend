using Application.DTOs.Admin;
using Application.Features.Admin.Notifications.Dto;

namespace Infrastructure.Services.Admin.Interfaces;

public interface IAdminNotificationService
{
    Task<PagedResultDto<AdminNotificationDto>> GetAllAsync(NotificationPageQueryDto query, CancellationToken cancellationToken);
    Task<AdminNotificationDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<SystemAlertResultDto> SendSystemAlertAsync(SystemAlertRequestDto request, AdminActorDto actor, CancellationToken cancellationToken);
}

