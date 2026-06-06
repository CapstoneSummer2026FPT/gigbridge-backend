using Application.Common.Interfaces;
using Application.Features.Admin.Users.Shared.DTOs;
using MediatR;

namespace Application.Features.Admin.Users.CreateNewUser.Commands;

public record CreateNewUserCommand(CreateNewUser.DTOs.CreateUserRequest Request) : IRequest<AdminUserDto>;
