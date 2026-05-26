using Application.DTOs.Admin;
using Application.Features.Admin.Users.Dto;
using MediatR;

namespace Application.Features.Admin.Users.Command;

public sealed record ChangeUserStatusCommand(
    Guid UserId,
    UserStatusRequestDto Request,
    AdminActorDto Actor) : IRequest<AdminUserDetailDto>;

