using Application.Features.Auth.DTOs.AuthDTOs;

namespace Application.Common.Interfaces;
public interface IGoogleAuthService 
{
  Task<GoogleUserInfoDTO> VerifyAuthCodeAsync(string authCode);

}