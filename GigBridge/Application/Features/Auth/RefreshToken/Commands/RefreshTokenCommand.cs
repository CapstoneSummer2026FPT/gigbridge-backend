using Application.Features.Auth.Shared.DTOs;
using MediatR;

namespace Application.Features.Auth.RefreshToken.Commands;

public record RefreshTokenCommand(
    string AccessToken,
    string RefreshToken,
    IReadOnlyCollection<string>? RefreshTokenCandidates = null)
    : IRequest<(LoginResponse LoginData, string RefreshToken, DateTime RefreshTokenExpiry)>;
