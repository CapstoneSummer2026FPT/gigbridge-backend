using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Profiles.FreelancerProfile.CreateFreelancerProfile.DTOs;
using Application.Features.Profiles.FreelancerProfile.Common;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FreelancerProfileEntity = Domain.Entities.FreelancerProfile;

namespace Application.Features.Profiles.FreelancerProfile.UpdateFreelancerProfile.Commands;

public class UpdateFreelancerProfileCommandHandler
    : IRequestHandler<UpdateFreelancerProfileCommand, FreelancerProfileResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;
    private readonly ILogger<UpdateFreelancerProfileCommandHandler> _logger;

    public UpdateFreelancerProfileCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IMapper mapper,
        ILogger<UpdateFreelancerProfileCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<FreelancerProfileResponseDto> Handle(
        UpdateFreelancerProfileCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUserService.UserId) || !Guid.TryParse(_currentUserService.UserId, out var currentUserId))
        {
            throw new BadRequestException("User ID from token is invalid or missing.");
        }

        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);

        var user = await _context.Set<User>()
            .Include(u => u.FreelancerProfile)
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
        await FreelancerProfileTaxonomy.SynchronizeSelectionsAsync(
            _context,
            freelancerProfile,
            request.Dto.MajorId,
            taxonomyMappings,
            now,
            cancellationToken);

        if (request.Dto.SkillIds is not null)
        {
            var skills = await FreelancerProfileSkills.ValidateAndLoadAsync(
                _context,
                request.Dto.SkillIds,
                cancellationToken);
            await FreelancerProfileSkills.SynchronizeAsync(
                _context,
                freelancerProfile,
                skills,
                cancellationToken);
        }

        freelancerProfile.ProfileCompletionScore = FreelancerProfileTaxonomy.CalculateCompletionScore(freelancerProfile);

        if (FreelancerProfileTaxonomy.IsSetupComplete(freelancerProfile))
        {
            user.IsSetup = true;
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            foreach (var entry in exception.Entries)
            {
                var primaryKey = entry.Metadata.FindPrimaryKey();
                var key = primaryKey is null
                    ? "<none>"
                    : string.Join(
                        ", ",
                        primaryKey.Properties.Select(property =>
                            $"{property.Name}={entry.Property(property.Name).CurrentValue}"));
                _logger.LogWarning(
                    exception,
                    "Freelancer profile persistence conflict. Entity={EntityType}, State={EntityState}, Key={PrimaryKey}",
                    entry.Metadata.ClrType.Name,
                    entry.State,
                    key);
            }

            throw new ConflictException(
                "Your freelancer profile was updated by another request. Reload the latest profile and try again.",
                exception);
        }

        return _mapper.Map<FreelancerProfileResponseDto>(freelancerProfile);
    }
}
