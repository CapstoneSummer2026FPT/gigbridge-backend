using Application.Common.Interfaces;
using MediatR;

namespace Application.Features.Admin.Users.DeleteUser.Commands;

public record DeleteUserCommand(string Email) : IRequest<bool>;
