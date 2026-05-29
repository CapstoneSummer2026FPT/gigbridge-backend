using Application.Features.Admin.Users.CreateNewUser.DTOs;
using Application.Features.Admin.Users.GetAllUser.DTOs;
using Application.Features.Admin.Users.Shared.DTOs;
using Application.Features.Admin.Users.UpdateUser.DTOs;

namespace Application.Common.Interfaces.IService;

public interface IUserService
{
    Task<GetAllUsersResponse> GetAllAsync(int page, int pageSize, string? search, int? status, CancellationToken cancellationToken = default);
    Task<AdminUserDto?> GetClientByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<AdminUserDto?> GetFreelancerByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<AdminUserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<AdminUserDto?> UpdateAsync(string email, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task<bool> ToggleActivityAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string email, CancellationToken cancellationToken = default);
}
