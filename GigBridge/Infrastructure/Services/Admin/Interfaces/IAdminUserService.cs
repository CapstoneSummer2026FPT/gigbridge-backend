using Application.DTOs.Admin;
using Application.Features.Admin.Users.Dto;

namespace Infrastructure.Services.Admin.Interfaces;

public interface IAdminUserService
{
    Task<PagedResultDto<AdminUserDto>> GetAllAsync(UserPageQueryDto query, CancellationToken cancellationToken);
    Task<AdminUserDetailDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<AdminUserDetailDto> SetStatusAsync(Guid id, UserStatusRequestDto request, AdminActorDto actor, CancellationToken cancellationToken);
    Task<AdminUserDto> CreateAsync(SaveAdminUserRequestDto request, AdminActorDto actor, CancellationToken cancellationToken);
    Task<AdminUserDto> UpdateAsync(Guid id, SaveAdminUserRequestDto request, AdminActorDto actor, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, AdminActorDto actor, CancellationToken cancellationToken);
}

