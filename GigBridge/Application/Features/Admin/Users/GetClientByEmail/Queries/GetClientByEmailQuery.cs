using Application.Common.Interfaces;
using Application.Features.Admin.Users.Shared.DTOs;
using MediatR;

namespace Application.Features.Admin.Users.GetClientByEmail.Queries;

public record GetClientByEmailQuery(string Email) : IRequest<AdminUserDto?>, IRequireAdmin;
