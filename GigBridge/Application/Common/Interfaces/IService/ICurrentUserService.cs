namespace Application.Common.Interfaces.IService;
public interface ICurrentUserService {
    string? UserId { get; }
    string? Email { get; }
    string? Role { get; }
}