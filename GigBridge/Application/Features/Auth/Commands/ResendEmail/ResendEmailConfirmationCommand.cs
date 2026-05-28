using Application.Common.Interfaces.IService;
using Application.Features.Auth.DTOs;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Auth.Commands.ResendEmail;

public record ResendEmailConfirmationCommand(EmailResendConfirmationRequest Request) : IRequest;


