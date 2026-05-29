using Application.Features.Admin.Users.Shared.DTOs;

namespace Application.Features.Admin.Users.GetAllUser.DTOs;

public class GetAllUsersResponse
{
    public required IReadOnlyList<AdminUserDto> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
}
