using Application.Features.Auth.DTOs;

namespace Application.Common.Interfaces.IService;
public interface IGoogleAuthService 
{
  Task<GoogleUserInfoDTO> VerifyAuthCodeAsync(string authCode);

}