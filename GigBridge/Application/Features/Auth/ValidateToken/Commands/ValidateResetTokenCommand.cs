using Application.Features.Auth.ValidateToken.DTOs;
using MediatR;

namespace Application.Features.Auth.ValidateToken.Commands;

public record ValidateResetTokenCommand(ValidateResetTokenRequest Request) : IRequest<bool>;
