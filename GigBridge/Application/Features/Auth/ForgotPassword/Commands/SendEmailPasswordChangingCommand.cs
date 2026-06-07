using Application.Features.Auth.ForgotPassword.DTOs;
using MediatR;

namespace Application.Features.Auth.ForgotPassword.Commands;

public record SendEmailPasswordChangingCommand(ForgotPasswordRequest Request) : IRequest;
