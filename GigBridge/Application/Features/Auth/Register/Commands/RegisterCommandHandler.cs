using Application.Common.Domain;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Auth.Shared.DTOs;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.Register.Commands;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, UserDTO>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTimeService _dateTimeService;
    private readonly IAuthEmailSender _authEmailSender;
    private readonly IMapper _mapper;

    public RegisterCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IDateTimeService dateTimeService,
        IAuthEmailSender authEmailSender,
        IMapper mapper)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _dateTimeService = dateTimeService;
        _authEmailSender = authEmailSender;
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
            throw new InvalidOperationException("Email already exists");
        }

        var user = CreateUser(registerRequest.role, email, registerRequest.FullName, registerRequest.Password);

        _context.Set<User>().Add(user);
        await _context.SaveChangesAsync(cancellationToken);
        await _authEmailSender.SendVerificationEmailAsync(user.Email, user.EmailVerificationToken!, cancellationToken);

        return _mapper.Map<UserDTO>(user);
    }

    private User CreateUser(int role, string email, string? fullName, string password)
    {
        var now = _dateTimeService.UtcNow;
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = email,
            FullName = string.IsNullOrWhiteSpace(fullName) ? email : fullName.Trim(),
            Password = _passwordHasher.HashPassword(password),
            Role = role,
            IsEmailVerified = false,
            IsActive = true,
            CreatedAt = now,
            EmailVerificationToken = Guid.NewGuid().ToString(),
            TokenExpiry = now.AddHours(24)
        };

        UserProfileFactory.AttachProfileForRole(user, now);
        return user;
    }
}
