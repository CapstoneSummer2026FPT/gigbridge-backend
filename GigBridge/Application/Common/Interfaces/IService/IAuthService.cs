using System.Threading;
using System.Threading.Tasks;
using Application.Features.Auth.DTOs;

namespace Application.Common.Interfaces.IService;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<UserDTO> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<(LoginResponse loginData, string refreshToken)> LoginWithRefreshAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<(LoginResponse loginData, string refreshToken)> GoogleLoginWithRefreshAsync(string authCode, CancellationToken cancellationToken = default);

    Task<(LoginResponse loginData, string refreshToken)> RefreshTokenAsync(string expriedAccessToken, string refreshToken, CancellationToken cancellationToken = default);

    Task ResendEmailConfirmationAsync(EmailResendConfirmationRequest email, CancellationToken cancellationToken = default);

    Task SendEmailPasswordChangingRequestAsync(EmailResendConfirmationRequest email, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);

    Task<bool> IsTokenExpired(string token, CancellationToken cancellationToken = default);
}