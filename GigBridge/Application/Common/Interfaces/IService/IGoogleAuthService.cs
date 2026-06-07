using System.Threading;
using System.Threading.Tasks;
using Application.Features.Auth.GoogleLogin.DTOs;

namespace Application.Common.Interfaces.IService;

public interface IGoogleAuthService
{
    Task<GoogleUserInfoDTO> VerifyAuthCodeAsync(string authCode, CancellationToken cancellationToken = default);
}