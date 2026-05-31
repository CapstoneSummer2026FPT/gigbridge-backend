using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Admin.Users.Shared.DTOs;
using Application.Features.Admin.Users.UpdateUser.DTOs;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Users.UpdateUser.Commands;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, AdminUserDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IMapper _mapper;

    public UpdateUserCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IMapper mapper)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _mapper = mapper;
    }

    public async Task<AdminUserDto?> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var user = await _context.Set<User>()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            return null;
        }

        ApplyUpdates(user, request.Request);
        user.UpdatedAt = _dateTimeService.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return _mapper.Map<AdminUserDto>(user);
    }

    private static void ApplyUpdates(User user, UpdateUserRequest request)
    {
        user.FullName = request.FullName ?? user.FullName;
        user.PhoneNumber = request.PhoneNumber ?? user.PhoneNumber;
        user.Avatar = request.Avatar ?? user.Avatar;
        user.PreferredLanguage = request.PreferredLanguage ?? user.PreferredLanguage;
        user.IsActive = request.IsActive ?? user.IsActive;
    }
}
