using Application.Common.InternalServices.Auth.Models;
using System.Threading;
using System.Threading.Tasks;
using Application.Features.Auth.GoogleLogin.DTOs;

namespace Application.Common.InternalServices.Auth.Interfaces;
public interface IGoogleAuthService
{
    Task<GoogleUserInfoDTO> VerifyAuthCodeAsync(string authCode, CancellationToken cancellationToken = default);
}