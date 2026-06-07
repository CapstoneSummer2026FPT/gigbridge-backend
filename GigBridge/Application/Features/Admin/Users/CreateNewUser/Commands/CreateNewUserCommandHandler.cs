using Application.Common.Domain;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Admin.Users.CreateNewUser.DTOs;
using Application.Features.Admin.Users.Shared.DTOs;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Users.CreateNewUser.Commands;

public class CreateNewUserCommandHandler : IRequestHandler<CreateNewUserCommand, AdminUserDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTimeService _dateTimeService;
    private readonly IMapper _mapper;

    public CreateNewUserCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IDateTimeService dateTimeService,
        IMapper mapper)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _dateTimeService = dateTimeService;
        _mapper = mapper;
    }

    public async Task<AdminUserDto> Handle(CreateNewUserCommand request, CancellationToken cancellationToken)
    {
        var createRequest = request.Request;
        var email = createRequest.Email.Trim();

        var emailExists = await _context.Set<User>()
            .AnyAsync(user => user.Email.ToLower() == email.ToLower(), cancellationToken);

        if (emailExists)
        {
            throw new InvalidOperationException("Email already exists");
        }

        var user = CreateUser(createRequest, email);
        _context.Set<User>().Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        return _mapper.Map<AdminUserDto>(user);
    }

    private User CreateUser(CreateUserRequest request, string email)
    {
        var now = _dateTimeService.UtcNow;
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = email,
            Password = _passwordHasher.HashPassword(request.Password),
            Role = request.Role,
            PhoneNumber = request.PhoneNumber,
            IsEmailVerified = false,
            IsActive = true,
            CreatedAt = now
        };

        UserProfileFactory.AttachProfileForRole(user, now);
        return user;
    }
}
