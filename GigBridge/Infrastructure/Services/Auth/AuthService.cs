using Application.Common.Interfaces;
using Application.Common.Interfaces.IRepository;
using Application.Features.Auth.DTOs;
using AutoMapper;
using Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
namespace Infrastructure.Services.Auth;
public class AuthService : IAuthService {
    private readonly IJwtService _jwtService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly IConfiguration _configuration;
    public AuthService(IJwtService jwtService, IUnitOfWork unitOfWork) {
        _jwtService = jwtService;
        _unitOfWork = unitOfWork;
    }
    public async Task<LoginResponse> LoginAsync(LoginRequest request) {

        try 
        {
            var user = await _unitOfWork.UserRepository.GetAsync(c => c.FullName == request.Username);

            if (user == null || user.Password != request.Password)
            {
                return Task.FromResult<string?>(null);
            }

            return new LoginResponse
            {
                User = new UserDTO
                {
                    FullName = user.FullName,
                    Email = user.Email,
                    a = user.Avatar,
                    PhoneNumber = user.PhoneNumber,
                    Role = user.Role,
                    IsEmailVerified = user.IsEmailVerified,
                    IsActive = user.IsActive,
                    PreferredLanguage = user.PreferredLanguage,
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = user.UpdatedAt
                },
                Token = _jwtService.GenerateToken(user)
            };
        }
        catch(Exception ex) 
        {
            // Log the exception (you can use a logging framework here)
            Console.WriteLine($"An error occurred during login: {ex.Message}");
            return null; // Return null or an appropriate response indicating failure
        }
       
    }


}
