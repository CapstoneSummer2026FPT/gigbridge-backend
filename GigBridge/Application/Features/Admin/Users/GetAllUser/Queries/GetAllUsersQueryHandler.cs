using Application.Common.Interfaces;
using Application.Features.Admin.Users.GetAllUser.DTOs;
using Application.Features.Admin.Users.Shared.DTOs;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Users.GetAllUser.Queries;

public class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, GetAllUsersResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetAllUsersQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<GetAllUsersResponse> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var query = ApplyFilters(_context.Set<User>().AsNoTracking(), request.Search, request.Status);
        var total = await query.CountAsync(cancellationToken);

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new GetAllUsersResponse
        {
            Items = _mapper.Map<IReadOnlyList<AdminUserDto>>(users),
            Page = page,
            PageSize = pageSize,
            TotalItems = total
        };
    }

    private static IQueryable<User> ApplyFilters(IQueryable<User> query, string? search, int? status)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(keyword) || u.Email.ToLower().Contains(keyword));
        }

        return status switch
        {
            1 => query.Where(u => u.IsActive),
            0 => query.Where(u => !u.IsActive),
            _ => query
        };
    }
}
