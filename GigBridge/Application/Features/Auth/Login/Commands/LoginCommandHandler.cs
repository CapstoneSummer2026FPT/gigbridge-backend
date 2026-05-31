using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Auth.Shared.DTOs;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.Login.Commands;

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse?>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly IMapper _mapper;

    public LoginCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        IMapper mapper)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _mapper = mapper;
    }

    public async Task<LoginResponse?> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Set<User>()
            .Include(u => u.ClientProfile)
            .Include(u => u.FreelancerProfile)
            .FirstOrDefaultAsync(u => u.Email == request.LoginRequest.Email, cancellationToken);

        if (user is null || !_passwordHasher.VerifyPassword(request.LoginRequest.Password, user.Password))
        {
            return null;
        }

        EnsureUserCanLogin(user);

        return new LoginResponse
        {
            User = _mapper.Map<UserDTO>(user),
            Token = _jwtService.GenerateToken(user)
        };
    }

    private static void EnsureUserCanLogin(User user)
    {
        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("Your account has been suspended by the administrator");
        }

        if (!user.IsEmailVerified)
        {
            throw new UnauthorizedAccessException("Account has not been verified");
        }
    }
}
