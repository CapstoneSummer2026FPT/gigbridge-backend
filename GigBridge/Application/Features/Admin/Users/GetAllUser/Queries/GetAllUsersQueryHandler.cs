using Application.Common.Interfaces;
using Application.Features.Admin.Users.GetAllUser.DTOs;
using Application.Features.Admin.Users.Shared.DTOs;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
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

        var items = _mapper.Map<IReadOnlyList<AdminUserDto>>(users);
        await AddOpenReportCountsAsync(items, cancellationToken);
        var reportedUserCount = await query.CountAsync(user =>
            _context.Set<Report>().Any(report =>
                report.ReportedEntityId == user.UserId &&
                report.ReportedEntityType.ToLower() == ReportedEntityTypes.User.ToLower() &&
                (report.Status == (int)ReportStatus.Pending || report.Status == (int)ReportStatus.Reviewing)),
            cancellationToken);

        return new GetAllUsersResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            ReportedUserCount = reportedUserCount
        };
    }

    private async Task AddOpenReportCountsAsync(IReadOnlyList<AdminUserDto> users, CancellationToken cancellationToken)
    {
        if (users.Count == 0)
        {
            return;
        }

        var userIds = users.Select(user => user.UserId).ToArray();
        var openReportCounts = await _context.Set<Report>()
            .AsNoTracking()
            .Where(report =>
                report.ReportedEntityType.ToLower() == ReportedEntityTypes.User.ToLower() &&
                userIds.Contains(report.ReportedEntityId) &&
                (report.Status == (int)ReportStatus.Pending || report.Status == (int)ReportStatus.Reviewing))
            .GroupBy(report => report.ReportedEntityId)
            .Select(group => new { UserId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.UserId, item => item.Count, cancellationToken);

        foreach (var user in users)
        {
            user.OpenReportCount = openReportCounts.TryGetValue(user.UserId, out var count) ? count : 0;
            user.IsCurrentlyReported = user.OpenReportCount > 0;
        }
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
