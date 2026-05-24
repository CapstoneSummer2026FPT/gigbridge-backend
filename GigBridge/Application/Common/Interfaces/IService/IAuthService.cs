using System.Threading.Tasks;
using Application.Features.Auth.DTOs;

namespace Application.Common.Interfaces.IService;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);

    Task<UserDTO> RegisterAsync(RegisterRequest request);

    Task<(LoginResponse loginData , string refreshToken)> LoginWithRefreshAsync(LoginRequest request);

    Task<(LoginResponse loginData, string refreshToken)> GoogleLoginWithRefreshAsync(string authCode);

    Task<(LoginResponse loginData, string refreshToken)> RefreshTokenAsync(string expriedAccessToken, string refreshToken);

    Task ResendEmailConfirmationAsync(EmailResendConfirmationRequest email);

    Task SendEmailPasswordChangingRequestAsync(EmailResendConfirmationRequest email);

    Task ResetPasswordAsync(ResetPasswordRequest request);

    Task<bool> IsTokenExpired(string token);



}