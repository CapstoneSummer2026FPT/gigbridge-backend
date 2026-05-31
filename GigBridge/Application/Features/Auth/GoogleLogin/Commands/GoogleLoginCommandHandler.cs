using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Auth.GoogleLogin.DTOs;
using Application.Features.Auth.Shared.DTOs;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.GoogleLogin.Commands;

public class GoogleLoginCommandHandler : IRequestHandler<GoogleLoginCommand, (LoginResponse LoginData, string RefreshToken)>
{
    private readonly IApplicationDbContext _context;
    private readonly IGoogleAuthService _googleAuthService;
    private readonly IJwtService _jwtService;
    private readonly IDateTimeService _dateTimeService;
    private readonly IMapper _mapper;

    public GoogleLoginCommandHandler(
        IApplicationDbContext context,
        IGoogleAuthService googleAuthService,
        IJwtService jwtService,
        IDateTimeService dateTimeService,
        IMapper mapper)
    {
        _context = context;
        _googleAuthService = googleAuthService;
        _jwtService = jwtService;
        _dateTimeService = dateTimeService;
        _mapper = mapper;
    }

        public async Task<(LoginResponse LoginData, string RefreshToken)> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
        {
            return await _authService.GoogleLoginWithRefreshAsync(request.AuthCode, request.Role, cancellationToken);
        }
    }
}
