using Application.DTOs.Admin;
using Application.Features.Admin.Users.Dto;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Entities;
using Infrastructure.Services.Admin.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Admin;

public sealed class AdminUserService : AdminServiceBase, IAdminUserService
{
    private readonly IMapper _mapper;

    public AdminUserService(IApplicationDbContext dbContext, IMapper mapper) : base(dbContext)
    {
        _mapper = mapper;
    }

    public async Task<PagedResultDto<AdminUserDto>> GetAllAsync(UserPageQueryDto query, CancellationToken cancellationToken)
    {
        ValidatePaging(query);
        var users = DbContext.Set<User>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            users = users.Where(x =>
                x.FullName.ToLower().Contains(search) ||
                x.Email.ToLower().Contains(search) ||
                (x.PhoneNumber != null && x.PhoneNumber.ToLower().Contains(search)));
        }

        if (query.IsActive.HasValue)
        {
            users = users.Where(x => x.IsActive == query.IsActive);
        }

        if (query.Role.HasValue)
        {
            users = users.Where(x => x.Role == query.Role);
        }

        users = FilterDates(users, query, x => x.CreatedAt);
        return await ToPageAsync(users.OrderByDescending(x => x.CreatedAt)
            .ProjectTo<AdminUserDto>(_mapper.ConfigurationProvider), query, cancellationToken);
    }

    public async Task<AdminUserDetailDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await DbContext.Set<User>().AsNoTracking().Where(x => x.UserId == id)
            .ProjectTo<AdminUserDetailDto>(_mapper.ConfigurationProvider).SingleOrDefaultAsync(cancellationToken);
        return result ?? throw new NotFoundException(nameof(User), id);
    }

    public async Task<AdminUserDetailDto> SetStatusAsync(
        Guid id,
        UserStatusRequestDto request,
        AdminActorDto actor,
        CancellationToken cancellationToken)
    {
        if (!request.IsActive && id == actor.AdminId)
        {
            throw Invalid("userId", "An administrator cannot disable their own account.");
        }

        if (!request.IsActive && string.IsNullOrWhiteSpace(request.Reason))
        {
            throw Invalid("reason", "A reason is required when disabling a user account.");
        }

        var user = await DbContext.Set<User>().SingleOrDefaultAsync(x => x.UserId == id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), id);
        var oldValues = new { user.IsActive, user.UpdatedAt };
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        AddAudit(actor, "UserStatusChanged", id, "User", oldValues,
            new { user.IsActive, user.UpdatedAt, Reason = request.Reason?.Trim() });
        await DbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<AdminUserDto> CreateAsync(SaveAdminUserRequestDto request, AdminActorDto actor, CancellationToken cancellationToken)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            Role = request.Role,
            IsEmailVerified = request.IsEmailVerified,
            PreferredLanguage = request.PreferredLanguage?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var entityEntry = DbContext.Set<User>().Add(user);
        AddAudit(actor, "UserCreated", user.UserId, "User", null, Values(user));
        await DbContext.SaveChangesAsync(cancellationToken);
        return _mapper.Map<AdminUserDto>(user);
    }

    public async Task<AdminUserDto> UpdateAsync(Guid id, SaveAdminUserRequestDto request, AdminActorDto actor, CancellationToken cancellationToken)
    {
        var user = await DbContext.Set<User>().SingleOrDefaultAsync(x => x.UserId == id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), id);
        var oldValues = Values(user);
        user.FullName = request.FullName.Trim();
        user.Email = request.Email.Trim();
        user.PhoneNumber = request.PhoneNumber?.Trim();
        user.Role = request.Role;
        user.IsEmailVerified = request.IsEmailVerified;
        user.PreferredLanguage = request.PreferredLanguage?.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        AddAudit(actor, "UserUpdated", id, "User", oldValues, Values(user));
        await DbContext.SaveChangesAsync(cancellationToken);
        return _mapper.Map<AdminUserDto>(user);
    }

    public async Task DeleteAsync(Guid id, AdminActorDto actor, CancellationToken cancellationToken)
    {
        var user = await DbContext.Set<User>().SingleOrDefaultAsync(x => x.UserId == id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), id);
        var oldValues = Values(user);
        DbContext.Set<User>().Remove(user);
        AddAudit(actor, "UserDeleted", id, "User", oldValues, null);
        await DbContext.SaveChangesAsync(cancellationToken);
    }

    private object Values(User user)
    {
        return new
        {
            user.FullName,
            user.Email,
            user.PhoneNumber,
            user.Role,
            user.IsEmailVerified,
            user.PreferredLanguage,
            user.IsActive,
            user.UpdatedAt
        };
    }
}

