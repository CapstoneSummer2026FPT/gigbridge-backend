using Application.Features.Auth.Shared.DTOs;
using MediatR;

namespace Application.Features.Auth.GoogleLogin.Commands;

public record GoogleLoginCommand(string AuthCode, int? Role, bool? IsFromSignIn) : IRequest<(LoginResponse LoginData, string RefreshToken, DateTime RefreshTokenExpiry)>;
