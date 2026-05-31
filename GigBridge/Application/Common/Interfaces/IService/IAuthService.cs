using System.Threading;
using System.Threading.Tasks;
using Application.Features.Auth.Shared.DTOs;
using Application.Features.Auth.Login.DTOs;
using Application.Features.Auth.Register.DTOs;
using Application.Features.Auth.ResendEmail.DTOs;
using Application.Features.Auth.ForgotPassword.DTOs;
using Application.Features.Auth.ResetPassword.DTOs;
using Application.Features.Auth.SendOtp.DTOs;
using Application.Features.Auth.VerifyOtp.DTOs;

namespace Application.Common.Interfaces.IService;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<UserDTO> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<(LoginResponse loginData, string refreshToken)> LoginWithRefreshAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<(LoginResponse loginData, string refreshToken)> GoogleLoginWithRefreshAsync(string authCode, int? role, CancellationToken cancellationToken = default);

    Task<(LoginResponse loginData, string refreshToken)> RefreshTokenAsync(string expriedAccessToken, string refreshToken, CancellationToken cancellationToken = default);

    Task ResendEmailConfirmationAsync(EmailResendConfirmationRequest email, CancellationToken cancellationToken = default);

    Task SendEmailPasswordChangingRequestAsync(ForgotPasswordRequest email, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);

    Task<bool> IsTokenExpired(string token, CancellationToken cancellationToken = default);

    Task SendOtpAsync(SendOtpRequest request, CancellationToken cancellationToken = default);

    Task VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default);
}