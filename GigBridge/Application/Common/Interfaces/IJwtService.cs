namespace Application.Common.Interfaces;
public interface IJwtService {
    string GenerateToken(string username);
}