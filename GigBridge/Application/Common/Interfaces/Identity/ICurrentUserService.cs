namespace Application.Common.Interfaces.Identity;
public interface ICurrentUserService {
    string? UserId { get; }
    string? Email { get; }
    string? Role { get; }
}