using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Profile.UpdateFreelancerProfile.DTOs;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Profile.UpdateFreelancerProfile.Commands
{
    public class UpdateFreelancerProfileCommandHandler : IRequestHandler<UpdateFreelancerProfileCommand, FreelancerProfileResponseDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public UpdateFreelancerProfileCommandHandler(
            IApplicationDbContext context,
            ICurrentUserService currentUserService,
            IMapper mapper)
        {
            _context = context;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }

        public async Task<FreelancerProfileResponseDto> Handle(UpdateFreelancerProfileCommand request, CancellationToken cancellationToken)
        {
            var userIdString = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
                throw new UnauthorizedAccessException("User not authenticated");

            // Get or create freelancer profile
            var freelancerProfile = await _context.Set<FreelancerProfile>()
                .FirstOrDefaultAsync(fp => fp.UserId == userId, cancellationToken);

            if (freelancerProfile == null)
            {
                // Create new freelancer profile
                freelancerProfile = new FreelancerProfile
                {
                    FreelancerProfilesId = Guid.NewGuid(),
                    UserId = userId,
                    Title = request.Data.Title,
                    Bio = request.Data.Bio,
                    HourlyRate = request.Data.HourlyRate,
                    ExperienceLevel = request.Data.ExperienceLevel,
                    Availability = request.Data.Availability,
                    Location = request.Data.Location,
                    ProfileCompletionScore = 50,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Set<FreelancerProfile>().Add(freelancerProfile);
            }
            else
            {
                // Update existing freelancer profile
                freelancerProfile.Title = request.Data.Title;
                freelancerProfile.Bio = request.Data.Bio;
                freelancerProfile.HourlyRate = request.Data.HourlyRate;
                freelancerProfile.ExperienceLevel = request.Data.ExperienceLevel;
                freelancerProfile.Availability = request.Data.Availability;
                freelancerProfile.Location = request.Data.Location;
                freelancerProfile.UpdatedAt = DateTime.UtcNow;

                _context.Set<FreelancerProfile>().Update(freelancerProfile);
            }

            // Update User.IsSetup to true
            var user = await _context.Set<User>()
                .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            
            if (user != null && !user.IsSetup)
            {
                user.IsSetup = true;
                user.UpdatedAt = DateTime.UtcNow;
                _context.Set<User>().Update(user);
            }

            await _context.SaveChangesAsync(cancellationToken);

            return _mapper.Map<FreelancerProfileResponseDto>(freelancerProfile);
        }
    }
}
