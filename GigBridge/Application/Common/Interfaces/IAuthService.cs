using System.Threading.Tasks;
using Application.Features.Auth.DTOs.AuthDTOs;

namespace Application.Common.Interfaces;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
}