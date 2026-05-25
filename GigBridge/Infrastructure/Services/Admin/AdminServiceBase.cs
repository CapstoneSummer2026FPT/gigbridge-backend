using System.Linq.Expressions;
using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.DTOs.Admin;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Admin;

public abstract class AdminServiceBase
{
    protected AdminServiceBase(IApplicationDbContext dbContext)
    {
        DbContext = dbContext;
    }

    protected IApplicationDbContext DbContext { get; }

    protected void AddAudit(AdminActorDto actor, string action, Guid? entityId, string entityType, object? oldValues, object? newValues)
    {
        DbContext.Set<AdminAuditLog>().Add(new AdminAuditLog
        {
            AdminAuditLogsId = Guid.NewGuid(),
            AdminId = actor.AdminId,
            Action = action,
            EntityId = entityId,
            EntityType = entityType,
            OldValues = oldValues == null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues == null ? null : JsonSerializer.Serialize(newValues),
            IpAddress = actor.IpAddress,
            UserAgent = actor.UserAgent,
            CreatedAt = DateTime.UtcNow
        });
    }

    protected static ValidationException Invalid(string name, string message) =>
        new(new Dictionary<string, string[]> { [name] = [message] });

    protected static void ValidatePaging(PagedQueryDto query)
    {
        var errors = new Dictionary<string, string[]>();
        if (query.Page < 1)
        {
            errors["page"] = ["Page must be greater than or equal to 1."];
        }

        if (query.PageSize is < 1 or > 100)
        {
            errors["pageSize"] = ["Page size must be between 1 and 100."];
        }

        if (query.From.HasValue && query.To.HasValue && query.From > query.To)
        {
            errors["dateRange"] = ["From must be before or equal to to."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    protected static IQueryable<TEntity> FilterDates<TEntity>(
        IQueryable<TEntity> query,
        PagedQueryDto filter,
        Expression<Func<TEntity, DateTime>> dateSelector)
    {
        if (!filter.From.HasValue && !filter.To.HasValue)
        {
            return query;
        }

        var parameter = dateSelector.Parameters[0];
        Expression? condition = null;
        if (filter.From.HasValue)
        {
            condition = Expression.GreaterThanOrEqual(dateSelector.Body, Expression.Constant(filter.From.Value));
        }

        if (filter.To.HasValue)
        {
            var upper = Expression.LessThanOrEqual(dateSelector.Body, Expression.Constant(filter.To.Value));
            condition = condition == null ? upper : Expression.AndAlso(condition, upper);
        }

        return query.Where(Expression.Lambda<Func<TEntity, bool>>(condition!, parameter));
    }

    protected static async Task<PagedResultDto<T>> ToPageAsync<T>(
        IQueryable<T> query,
        PagedQueryDto paging,
        CancellationToken cancellationToken)
    {
        var count = await query.CountAsync(cancellationToken);
        var items = await query.Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize).ToListAsync(cancellationToken);
        return new PagedResultDto<T>
        {
            Items = items,
            Page = paging.Page,
            PageSize = paging.PageSize,
            TotalItems = count
        };
    }
}
