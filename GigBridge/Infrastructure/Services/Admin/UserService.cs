using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Admin.Users.CreateNewUser.DTOs;
using Application.Features.Admin.Users.GetAllUser.DTOs;
using Application.Features.Admin.Users.Shared.DTOs;
using Application.Features.Admin.Users.UpdateUser.DTOs;
using AutoMapper;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Admin;

public class UserService : IUserService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(IApplicationDbContext dbContext, IMapper mapper, IPasswordHasher passwordHasher)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _passwordHasher = passwordHasher;
    }

    public async Task<GetAllUsersResponse> GetAllAsync(int page, int pageSize, string? search, int? status, CancellationToken cancellationToken)
    {
        var query = _dbContext.Set<User>().AsQueryable();

        if (search is not null)
            query = query.Where(u => u.FullName.Contains(search) || u.Email.Contains(search));

        if (status is not null)
            query = query.Where(u => status.Value == 1 ? u.IsActive : !u.IsActive);

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

    public async Task<AdminUserDto?> GetClientByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Set<User>()
            .FirstOrDefaultAsync(u => u.Email == email && u.Role == 0, cancellationToken);

        return user is null ? null : _mapper.Map<AdminUserDto>(user);
    }

    public async Task<AdminUserDto?> GetFreelancerByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Set<User>()
            .FirstOrDefaultAsync(u => u.Email == email && u.Role == 1, cancellationToken);

        return user is null ? null : _mapper.Map<AdminUserDto>(user);
    }

    public async Task<AdminUserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var hash = _passwordHasher.HashPassword(request.Password);

        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = request.FullName,
            Email = request.Email,
            Password = hash,
            Role = request.Role,
            PhoneNumber = request.PhoneNumber,
            IsEmailVerified = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Set<User>().Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return _mapper.Map<AdminUserDto>(user);
    }

    public async Task<AdminUserDto?> UpdateAsync(string email, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Set<User>()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
            return null;

        if (request.FullName is not null)
            user.FullName = request.FullName;

        if (request.PhoneNumber is not null)
            user.PhoneNumber = request.PhoneNumber;

        if (request.Avatar is not null)
            user.Avatar = request.Avatar;

        if (request.PreferredLanguage is not null)
            user.PreferredLanguage = request.PreferredLanguage;

        if (request.IsActive.HasValue)
            user.IsActive = request.IsActive.Value;

        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return _mapper.Map<AdminUserDto>(user);
    }

    public async Task<bool> ToggleActivityAsync(string email, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Set<User>()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
            return false;

        user.IsActive = !user.IsActive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteAsync(string email, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Set<User>()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
            return false;

        _dbContext.Set<User>().Remove(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
