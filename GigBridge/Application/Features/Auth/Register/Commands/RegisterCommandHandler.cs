using Application.Common.Domain;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Auth.Shared.DTOs;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.Register.Commands;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, UserDTO>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTimeService _dateTimeService;
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;

    public RegisterCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IDateTimeService dateTimeService,
        ICacheService cacheService,
        IMapper mapper)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _dateTimeService = dateTimeService;
        _cacheService = cacheService;
        _mapper = mapper;
    }

    public async Task<UserDTO> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var registerRequest = request.RegisterRequest;
        var email = registerRequest.Email.Trim();

        var emailExists = await _context.Set<User>()
            .AnyAsync(user => user.Email.ToLower() == email.ToLower(), cancellationToken);

        if (emailExists)
        {
            throw new BadRequestException("Email already exists");
        }

        var verificationKey = $"verified_email:{email.ToLowerInvariant()}";
        var cachedOtp = await _cacheService.GetAsync<string>(verificationKey, cancellationToken);
        if (string.IsNullOrEmpty(cachedOtp))
        {
            throw new BadRequestException("Email has not been verified or verification has expired.");
        }

        var user = CreateUser(registerRequest.role!.Value, email, registerRequest.FullName, registerRequest.Password);

        _context.Set<User>().Add(user);
        await _context.SaveChangesAsync(cancellationToken);
        await _cacheService.RemoveAsync(verificationKey, cancellationToken);

        return _mapper.Map<UserDTO>(user);
    }

    private User CreateUser(UserRole role, string email, string? fullName, string password)
    {
        var now = _dateTimeService.UtcNow;
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = email,
            FullName = string.IsNullOrWhiteSpace(fullName) ? email : fullName.Trim(),
            Password = _passwordHasher.HashPassword(password),
            Role = (int)role,
            IsEmailVerified = true,
            IsActive = true,
            CreatedAt = now,
            EmailVerificationToken = null,
            TokenExpiry = null
        };

        UserProfileFactory.AttachProfileForRole(user, now);
        return user;
    }
}
