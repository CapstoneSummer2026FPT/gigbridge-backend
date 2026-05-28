using Application.Features.Auth.ResendEmail.DTOs;
using MediatR;

namespace Application.Features.Auth.ResendEmail.Commands;

public record ResendEmailConfirmationCommand(EmailResendConfirmationRequest Request) : IRequest;
