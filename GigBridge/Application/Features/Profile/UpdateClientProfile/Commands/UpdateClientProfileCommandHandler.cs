using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Profile.UpdateClientProfile.DTOs;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Profile.UpdateClientProfile.Commands
{
    public class UpdateClientProfileCommandHandler : IRequestHandler<UpdateClientProfileCommand, ClientProfileResponseDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public UpdateClientProfileCommandHandler(
            IApplicationDbContext context,
            ICurrentUserService currentUserService,
            IMapper mapper)
        {
            _context = context;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }

        public async Task<ClientProfileResponseDto> Handle(UpdateClientProfileCommand request, CancellationToken cancellationToken)
        {
            var userIdString = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
                throw new UnauthorizedAccessException("User not authenticated");

            // Get or create client profile
            var clientProfile = await _context.Set<ClientProfile>()
                .FirstOrDefaultAsync(cp => cp.UserId == userId, cancellationToken);

            if (clientProfile == null)
            {
                // Create new client profile
                clientProfile = new ClientProfile
                {
                    ClientProfilesId = Guid.NewGuid(),
                    UserId = userId,
                    CompanyName = request.Data.CompanyName,
                    CompanyWebsite = request.Data.CompanyWebsite,
                    CompanySize = request.Data.CompanySize,
                    Industry = request.Data.Industry,
                    CompanyDescription = request.Data.CompanyDescription,
                    Location = request.Data.Location,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Set<ClientProfile>().Add(clientProfile);
            }
            else
            {
                // Update existing client profile
                clientProfile.CompanyName = request.Data.CompanyName;
                clientProfile.CompanyWebsite = request.Data.CompanyWebsite;
                clientProfile.CompanySize = request.Data.CompanySize;
                clientProfile.Industry = request.Data.Industry;
                clientProfile.CompanyDescription = request.Data.CompanyDescription;
                clientProfile.Location = request.Data.Location;
                clientProfile.UpdatedAt = DateTime.UtcNow;

                _context.Set<ClientProfile>().Update(clientProfile);
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

            return _mapper.Map<ClientProfileResponseDto>(clientProfile);
        }
    }
}
