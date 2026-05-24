using Application.Common.Interfaces;
using Application.Common.Interfaces.IRepository;
using Application.Features.Auth.DTOs.AuthDTOs;
using AutoMapper;
using Domain.Entities;
using System;
using System.Threading.Tasks;

namespace Infrastructure.Services.Auth;

public class AuthService : IAuthService
{
    private readonly IJwtService _jwtService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IMapper _mapper;

    public AuthService(
        IJwtService jwtService,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IMapper mapper)
    {
        _jwtService = jwtService;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _mapper = mapper;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        try
        {
            // Query user by Email instead of FullName, and verify passwords using the injected IPasswordHasher
            var user = await _unitOfWork.UserRepository.GetAsync(c => c.Email == request.Email);

            if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.Password))
            {
                return null;
            }

            return new LoginResponse
            {
                User = _mapper.Map<UserDTO>(user),
                Token = _jwtService.GenerateToken(user)
            };
        }
        catch (Exception ex)
        {
            // Log the exception
            Console.WriteLine($"An error occurred during login: {ex.Message}");
            return null;
        }
    }
}
