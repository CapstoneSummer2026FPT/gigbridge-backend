using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Profiles.FreelancerProfile.CreateFreelancerProfile.DTOs;
using Application.Features.Profiles.FreelancerProfile.Common;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using FreelancerProfileEntity = Domain.Entities.FreelancerProfile;

namespace Application.Features.Profiles.FreelancerProfile.CreateFreelancerProfile.Commands;

public class CreateFreelancerProfileCommandHandler 
    : IRequestHandler<CreateFreelancerProfileCommand, FreelancerProfileResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;

    public CreateFreelancerProfileCommandHandler(
        IApplicationDbContext context, 
        ICurrentUserService currentUserService,
        IMapper mapper)
    {
        _context = context;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<FreelancerProfileResponseDto> Handle(
        CreateFreelancerProfileCommand request, 
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUserService.UserId) || !Guid.TryParse(_currentUserService.UserId, out var currentUserId))
        {
            throw new BadRequestException("User ID from token is invalid or missing.");
        }

        var user = await _context.Set<User>()
            .Include(u => u.FreelancerProfile)
                .ThenInclude(profile => profile!.FreelancerProfileCategories)
            .FirstOrDefaultAsync(u => u.UserId == currentUserId, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException(nameof(User), currentUserId);
        }

        var freelancerProfile = user.FreelancerProfile;
        var now = DateTime.UtcNow;

        if (freelancerProfile == null)
        {
            user.AttachProfileForRole(now);
            freelancerProfile = user.FreelancerProfile;
            
            if (freelancerProfile != null)
            {
                _context.Set<FreelancerProfileEntity>().Add(freelancerProfile);
            }
            else
            {
                throw new BadRequestException("Unable to attach freelancer profile to user.");
            }
        }

        // Map inputs
        freelancerProfile.Title = request.Dto.Title?.Trim();
        freelancerProfile.Bio = request.Dto.Bio?.Trim();
        freelancerProfile.Availability = request.Dto.Availability;
        freelancerProfile.Location = request.Dto.Location?.Trim();
        freelancerProfile.UpdatedAt = now;

        var taxonomyMappings = await FreelancerProfileTaxonomy.ValidateAndLoadAsync(
            _context,
            request.Dto.MajorId,
            request.Dto.CategoryIds,
            cancellationToken);
        FreelancerProfileTaxonomy.ReplaceSelections(
            _context,
            freelancerProfile,
            request.Dto.MajorId,
            taxonomyMappings,
            now);

        freelancerProfile.ProfileCompletionScore = FreelancerProfileTaxonomy.CalculateCompletionScore(freelancerProfile);

        if (FreelancerProfileTaxonomy.IsSetupComplete(freelancerProfile))
        {
            user.IsSetup = true;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return _mapper.Map<FreelancerProfileResponseDto>(freelancerProfile);
    }
}
