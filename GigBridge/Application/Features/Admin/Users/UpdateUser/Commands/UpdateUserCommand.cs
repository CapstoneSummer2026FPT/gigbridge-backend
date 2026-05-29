using Application.Features.Admin.Users.Shared.DTOs;
using MediatR;

namespace Application.Features.Admin.Users.UpdateUser.Commands;

public record UpdateUserCommand(string Email, UpdateUser.DTOs.UpdateUserRequest Request) : IRequest<AdminUserDto?>;
