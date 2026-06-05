using Application.Common.Interfaces;
using Application.Features.Auth.ChangePassword.DTOs;
using MediatR;

namespace Application.Features.Auth.ChangePassword.Commands;

public record ChangePasswordCommand(ChangePasswordProfileRequest Request) : IRequest, IRequireAuthentication;
