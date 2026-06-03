using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Domain;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Profiles.ClientProfile.CreateClientProfile.DTOs;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ClientProfileEntity = Domain.Entities.ClientProfile;

namespace Application.Features.Profiles.ClientProfile.CreateClientProfile.Handlers;

public class CreateClientProfileCommandHandler 
    : IRequestHandler<Commands.CreateClientProfileCommand, ClientProfileResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;

    public CreateClientProfileCommandHandler(
        IApplicationDbContext context, 
        ICurrentUserService currentUserService,
        IMapper mapper)
    {
        _context = context;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<ClientProfileResponseDto> Handle(
        Commands.CreateClientProfileCommand request, 
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUserService.UserId) || !Guid.TryParse(_currentUserService.UserId, out var currentUserId))
        {
            throw new BadRequestException("User ID from token is invalid or missing.");
        }

        var user = await _context.Set<User>()
            .Include(u => u.ClientProfile)
            .FirstOrDefaultAsync(u => u.UserId == currentUserId, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException(nameof(User), currentUserId);
        }

        var clientProfile = user.ClientProfile;
        var now = DateTime.UtcNow;

        if (clientProfile == null)
        {
            UserProfileFactory.AttachProfileForRole(user, now);
            clientProfile = user.ClientProfile;

            if (clientProfile != null)
            {
                _context.Set<ClientProfileEntity>().Add(clientProfile);
            }
            else
            {
                throw new BadRequestException("Unable to attach client profile to user.");
            }
        }

        // Map inputs
        clientProfile.CompanyName = request.Dto.CompanyName?.Trim();
        clientProfile.CompanyWebsite = request.Dto.CompanyWebsite?.Trim();
        clientProfile.CompanySize = request.Dto.CompanySize;
        clientProfile.Industry = request.Dto.Industry?.Trim();
        clientProfile.Location = request.Dto.Location?.Trim();
        clientProfile.CompanyDescription = request.Dto.CompanyDescription?.Trim();
        clientProfile.UpdatedAt = now;

        // Verify requirements to set IsSetup = true
        bool requirementsMet = !string.IsNullOrWhiteSpace(clientProfile.CompanyName) &&
                               !string.IsNullOrWhiteSpace(clientProfile.Industry) &&
                               !string.IsNullOrWhiteSpace(clientProfile.Location);

        if (requirementsMet)
        {
            user.IsSetup = true;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ClientProfileResponseDto>(clientProfile);
    }
}
