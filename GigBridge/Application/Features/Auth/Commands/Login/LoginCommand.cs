using Application.Features.Auth.DTOs.AuthDTOs;
using MediatR;
namespace Application.Features.Auth.Commands.Login;
public record LoginCommand(LoginRequest LoginRequest) : IRequest<LoginResponse?>;