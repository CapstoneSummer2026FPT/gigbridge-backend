using Application.Common.Interfaces;
using BC = BCrypt.Net.BCrypt;

namespace Infrastructure.Services.Auth;

public class BCryptPasswordHasher : IPasswordHasher
{
    public string HashPassword(string password)
    {
        return BC.HashPassword(password);
    }

    public bool VerifyPassword(string password, string hashedPassword)
    {
        // If hashedPassword is null (e.g., OAuth user without password), return false
        if (string.IsNullOrEmpty(hashedPassword))
        {
            return false;
        }
        
        return BC.Verify(password, hashedPassword);
    }
}
