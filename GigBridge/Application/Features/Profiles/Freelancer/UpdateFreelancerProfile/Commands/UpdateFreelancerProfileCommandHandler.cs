using Application.Common.InternalServices.Portfolios.Services;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Media;
using Application.Features.Portfolios.Common;
using Application.Features.Profiles.FreelancerProfile.Common.DTOs;
using Application.Features.Profiles.FreelancerProfile.Common;
using Application.Features.Profiles.FreelancerProfile.UpdateFreelancerProfile.DTOs;
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
    private readonly IMediaService _mediaService;

    public UpdateFreelancerProfileCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IMapper mapper,
        ILogger<UpdateFreelancerProfileCommandHandler> logger,
        IMediaService mediaService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _mapper = mapper;
        _logger = logger;
        _mediaService = mediaService;
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
                .ThenInclude(profile => profile!.PortfolioItems)
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
        freelancerProfile.AllowSearchEngineIndexing = request.Dto.AllowSearchEngineIndexing;
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

        IReadOnlyList<string> removedPortfolioImageUrls = Array.Empty<string>();
        if (request.Dto.PortfolioItems is not null)
        {
            removedPortfolioImageUrls = SynchronizePortfolioItems(
                freelancerProfile,
                request.Dto.PortfolioItems);
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

        foreach (var imageUrl in removedPortfolioImageUrls)
        {
            await PortfolioImageStorage.TryDeleteAsync(_mediaService, imageUrl, _logger);
        }

        return _mapper.Map<FreelancerProfileResponseDto>(freelancerProfile);
    }

    private IReadOnlyList<string> SynchronizePortfolioItems(
        FreelancerProfileEntity freelancerProfile,
        IReadOnlyCollection<UpdatePortfolioItemDto> requestedItems)
    {
        var requestedIds = requestedItems
            .Where(item => item.PortfolioItemId.HasValue)
            .Select(item => item.PortfolioItemId!.Value)
            .ToList();
        if (requestedIds.Distinct().Count() != requestedIds.Count)
        {
            throw new BadRequestException("Duplicate portfolio item IDs are not allowed.");
        }

        var existingById = freelancerProfile.PortfolioItems
            .ToDictionary(item => item.PortfolioItemsId);
        var unknownItemId = requestedIds.FirstOrDefault(id => !existingById.ContainsKey(id));
        if (unknownItemId != Guid.Empty)
        {
            throw new BadRequestException("A portfolio item does not belong to the current freelancer profile.");
        }

        var requestedIdSet = requestedIds.ToHashSet();
        var itemsToRemove = freelancerProfile.PortfolioItems
            .Where(item => !requestedIdSet.Contains(item.PortfolioItemsId))
            .ToList();
        var removedImageUrls = itemsToRemove
            .Select(item => item.ImageUrl)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Cast<string>()
            .ToList();
        if (itemsToRemove.Count > 0)
        {
            _context.Set<PortfolioItem>().RemoveRange(itemsToRemove);
            foreach (var item in itemsToRemove)
            {
                freelancerProfile.PortfolioItems.Remove(item);
            }
        }

        foreach (var requestedItem in requestedItems)
        {
            PortfolioItem portfolioItem;
            if (requestedItem.PortfolioItemId.HasValue)
            {
                portfolioItem = existingById[requestedItem.PortfolioItemId.Value];
            }
            else
            {
                portfolioItem = new PortfolioItem
                {
                    PortfolioItemsId = Guid.NewGuid(),
                    FreelancerId = freelancerProfile.FreelancerProfilesId,
                    Freelancer = freelancerProfile
                };
                freelancerProfile.PortfolioItems.Add(portfolioItem);
                _context.Set<PortfolioItem>().Add(portfolioItem);
            }

            portfolioItem.Title = requestedItem.Title.Trim();
            portfolioItem.Description = NormalizeOptional(requestedItem.Description);
            portfolioItem.ProjectUrl = NormalizeOptional(requestedItem.ProjectUrl);
            portfolioItem.ProjectDate = requestedItem.ProjectDate;
        }

        return removedImageUrls;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
