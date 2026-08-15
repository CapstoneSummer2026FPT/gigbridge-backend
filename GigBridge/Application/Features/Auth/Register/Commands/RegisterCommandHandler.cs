using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Caching;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Elo.Interfaces;
using Application.Common.InternalServices.Auth.Services;
using Application.Features.Auth.Shared.DTOs;
using AutoMapper;
using Domain.Entities;
using Domain.Enums.Accounts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.Register.Commands;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, UserDTO>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTimeService _dateTimeService;
    private readonly ICacheService _cacheService;
    private readonly IUserEloService _userEloService;
    private readonly IMapper _mapper;

    public RegisterCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IDateTimeService dateTimeService,
        ICacheService cacheService,
        IUserEloService userEloService,
        IMapper mapper)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _dateTimeService = dateTimeService;
        _cacheService = cacheService;
        _userEloService = userEloService;
        _mapper = mapper;
    }

    public async Task<UserDTO> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var registerRequest = request.RegisterRequest;
        if (registerRequest.role is not (UserRole.Client or UserRole.Freelancer))
        {
            throw new BadRequestException("Only Client or Freelancer registration is allowed.");
        }

        var email = EmailCanonicalizer.Canonicalize(registerRequest.Email);

        var emailExists = await _context.Set<User>()
            .AnyAsync(user => user.Email == email, cancellationToken);

        if (emailExists)
        {
            throw new BadRequestException("Email already exists");
        }

        var verificationKey = OtpSecurity.VerifiedKey(
            OtpPurpose.Signup,
            email,
            registerRequest.VerificationTicket);
        var isVerified = await _cacheService.GetAndRemoveAsync<bool>(
            verificationKey,
            cancellationToken);
        if (!isVerified)
        {
            throw new BadRequestException("Email has not been verified or verification has expired.");
        }

        var user = CreateUser(registerRequest.role.Value, email, registerRequest.FullName, registerRequest.Password);

        _context.Set<User>().Add(user);
        await _userEloService.InitializeNewUserAsync(user, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

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
            CreatedAt = now
        };

        user.AttachProfileForRole(now);
        return user;
    }
}
