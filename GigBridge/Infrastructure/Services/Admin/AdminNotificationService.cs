using Application.DTOs.Admin;
using Application.Features.Admin.Notifications.Dto;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Entities;
using Infrastructure.Services.Admin.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Admin;

public sealed class AdminNotificationService : AdminServiceBase, IAdminNotificationService
{
    private readonly IMapper _mapper;

    public AdminNotificationService(IApplicationDbContext dbContext, IMapper mapper) : base(dbContext)
    {
        _mapper = mapper;
    }

    public async Task<PagedResultDto<AdminNotificationDto>> GetAllAsync(
        NotificationPageQueryDto query,
        CancellationToken cancellationToken)
    {
        ValidatePaging(query);
        var notifications = DbContext.Set<Domain.Entities.Notification>().AsNoTracking();
        if (query.Type.HasValue)
        {
            notifications = notifications.Where(x => x.Type == query.Type);
        }

        if (query.UserId.HasValue)
        {
            notifications = notifications.Where(x => x.UserId == query.UserId);
        }

        if (query.IsRead.HasValue)
        {
            notifications = notifications.Where(x => x.IsRead == query.IsRead);
        }

        if (!string.IsNullOrWhiteSpace(query.ReferenceType))
        {
            var referenceType = query.ReferenceType.Trim().ToLower();
            notifications = notifications.Where(x =>
                x.ReferenceType != null && x.ReferenceType.ToLower() == referenceType);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            notifications = notifications.Where(x =>
                x.Title.ToLower().Contains(search) ||
                (x.Content != null && x.Content.ToLower().Contains(search)));
        }

        notifications = FilterDates(notifications, query, x => x.CreatedAt);
        return await ToPageAsync(notifications.OrderByDescending(x => x.CreatedAt)
            .ProjectTo<AdminNotificationDto>(_mapper.ConfigurationProvider), query, cancellationToken);
    }

    public async Task<AdminNotificationDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await DbContext.Set<Domain.Entities.Notification>().AsNoTracking().Where(x => x.NotificationsId == id)
            .ProjectTo<AdminNotificationDto>(_mapper.ConfigurationProvider).SingleOrDefaultAsync(cancellationToken);
        return result ?? throw new NotFoundException(nameof(Notification), id);
    }

    public async Task<SystemAlertResultDto> SendSystemAlertAsync(SystemAlertRequestDto request, AdminActorDto actor, CancellationToken cancellationToken)
    {
        var userIds = request.UserIds.Distinct().ToArray();
        foreach (var userId in userIds)
        {
            DbContext.Set<Domain.Entities.Notification>().Add(new Domain.Entities.Notification
            {
                NotificationsId = Guid.NewGuid(),
                UserId = userId,
                Type = 10,
                Title = request.Title!.Trim(),
                Content = request.Content,
                ReferenceId = request.ReferenceId,
                ReferenceType = request.ReferenceType,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        AddAudit(actor, "SystemAlertSent", request.ReferenceId, request.ReferenceType ?? "Notification", null,
            new { UserIds = userIds, Title = request.Title!.Trim(), request.ReferenceId, request.ReferenceType });
        await DbContext.SaveChangesAsync(cancellationToken);
        return new SystemAlertResultDto { RecipientCount = userIds.Length };
    }
}

