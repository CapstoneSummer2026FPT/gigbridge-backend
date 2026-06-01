using Application.Features.Auth.Shared.DTOs;
using MediatR;

namespace Application.Features.Auth.GoogleLogin.Commands;

public record GoogleLoginCommand(string AuthCode, int? Role) : IRequest<(LoginResponse LoginData, string RefreshToken)>;
