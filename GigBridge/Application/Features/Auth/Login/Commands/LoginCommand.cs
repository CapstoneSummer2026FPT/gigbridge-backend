using Application.Features.Auth.Login.DTOs;
using Application.Features.Auth.Shared.DTOs;
using MediatR;

namespace Application.Features.Auth.Login.Commands;

public record LoginCommand(LoginRequest LoginRequest) : IRequest<LoginResponse?>;
