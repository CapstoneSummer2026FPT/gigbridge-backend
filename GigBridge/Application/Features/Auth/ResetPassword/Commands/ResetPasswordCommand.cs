using Application.Features.Auth.ResetPassword.DTOs;
using MediatR;

namespace Application.Features.Auth.ResetPassword.Commands;

public record ResetPasswordCommand(ResetPasswordRequest Request) : IRequest;
