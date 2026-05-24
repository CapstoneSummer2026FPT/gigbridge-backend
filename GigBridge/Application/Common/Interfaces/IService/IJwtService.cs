using Domain.Entities;

namespace Application.Common.Interfaces.IService;

public interface IJwtService
{
    string GenerateToken(User user);
}