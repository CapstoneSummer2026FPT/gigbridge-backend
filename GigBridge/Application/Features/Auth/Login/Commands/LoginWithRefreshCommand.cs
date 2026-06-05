using Application.Features.Auth.Login.DTOs;
using Application.Features.Auth.Shared.DTOs;
using MediatR;

namespace Application.Features.Auth.Login.Commands;

public record LoginWithRefreshCommand(LoginRequest LoginRequest) : IRequest<(LoginResponse LoginData, string RefreshToken)>;
