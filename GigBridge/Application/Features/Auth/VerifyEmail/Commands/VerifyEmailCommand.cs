using Application.Features.Auth.VerifyEmail.DTOs;
using MediatR;

namespace Application.Features.Auth.VerifyEmail.Commands;

public record VerifyEmailCommand(VerifyEmailRequest VerifyEmailRequest) : IRequest;
