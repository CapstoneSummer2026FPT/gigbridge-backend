using Application.Features.Admin.Users.ClearUserSuspension.DTOs;
using Application.Features.Admin.Users.Shared.DTOs;
using MediatR;

namespace Application.Features.Admin.Users.ClearUserSuspension.Commands;

public record ClearUserSuspensionCommand(ClearUserSuspensionRequest Request) : IRequest<AdminUserDto?>;
