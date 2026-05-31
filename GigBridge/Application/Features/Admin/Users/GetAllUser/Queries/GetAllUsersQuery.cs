using Application.Common.Interfaces;
using Application.Features.Admin.Users.GetAllUser.DTOs;
using MediatR;

namespace Application.Features.Admin.Users.GetAllUser.Queries;

public record GetAllUsersQuery(int Page = 1, int PageSize = 20, string? Search = null, int? Status = null)
    : IRequest<GetAllUsersResponse>, IRequireAdmin;
