using Application.Features.Auth.DTOs;
using MediatR;

namespace Application.Features.Auth.Commands.Register;

public record RegisterCommand(RegisterRequest RegisterRequest) : IRequest<UserDTO>;
