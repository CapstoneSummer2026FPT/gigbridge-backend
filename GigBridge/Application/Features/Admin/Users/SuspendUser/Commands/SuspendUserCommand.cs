using Application.Features.Admin.Users.Shared.DTOs;
using Application.Features.Admin.Users.SuspendUser.DTOs;
using MediatR;

namespace Application.Features.Admin.Users.SuspendUser.Commands;

public record SuspendUserCommand(SuspendUserRequest Request) : IRequest<AdminUserDto?>;
