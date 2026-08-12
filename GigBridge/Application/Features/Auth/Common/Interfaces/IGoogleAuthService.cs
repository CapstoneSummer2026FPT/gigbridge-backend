using System.Threading;
using System.Threading.Tasks;
using Application.Features.Auth.GoogleLogin.DTOs;

namespace Application.Features.Auth.Common.Interfaces;

public interface IGoogleAuthService
{
    Task<GoogleUserInfoDTO> VerifyAuthCodeAsync(string authCode, CancellationToken cancellationToken = default);
}