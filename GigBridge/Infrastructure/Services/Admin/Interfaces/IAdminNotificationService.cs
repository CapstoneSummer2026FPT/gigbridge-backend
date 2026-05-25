using Application.DTOs.Admin;

namespace Infrastructure.Services.Admin.Interfaces;

public interface IAdminNotificationService
{
    Task<SystemAlertResultDto> SendSystemAlertAsync(SystemAlertRequestDto request, AdminActorDto actor, CancellationToken cancellationToken);
}
