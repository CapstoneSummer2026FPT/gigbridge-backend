using Application.Features.Auth.Register.DTOs;
using Application.Features.Auth.Shared.DTOs;
using MediatR;

namespace Application.Features.Auth.Register.Commands;

public record RegisterCommand(RegisterRequest RegisterRequest) : IRequest<UserDTO>;
