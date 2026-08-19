using Application.Common.InternalServices.Portfolios.Services;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Media;
using Application.Features.Portfolios.Common;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Portfolios.CreatePortfolioItem.Commands;

public sealed class CreatePortfolioItemCommandHandler
    : IRequestHandler<CreatePortfolioItemCommand, PortfolioItemDto>
{
    private const int MaximumPortfolioItems = 20;
    private readonly IApplicationDbContext _context;
    private readonly IMediaService _mediaService;
    private readonly ILogger<CreatePortfolioItemCommandHandler> _logger;

    public CreatePortfolioItemCommandHandler(
        IApplicationDbContext context,
        IMediaService mediaService,
        ILogger<CreatePortfolioItemCommandHandler> logger)
    {
        _context = context;
        _mediaService = mediaService;
        _logger = logger;
    }

    public async Task<PortfolioItemDto> Handle(
        CreatePortfolioItemCommand request,
        CancellationToken cancellationToken)
    {
        var profile = await _context.Set<FreelancerProfile>()
            .FirstOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken);
        if (profile is null)
        {
            throw new NotFoundException(nameof(FreelancerProfile), request.UserId);
        }

        var itemCount = await _context.Set<PortfolioItem>()
            .CountAsync(item => item.FreelancerId == profile.FreelancerProfilesId, cancellationToken);
        if (itemCount >= MaximumPortfolioItems)
        {
            throw new BadRequestException("A profile cannot contain more than 20 portfolio items.");
        }

        string? uploadedImageUrl = null;
        try
        {
            if (request.Image is not null)
            {
                uploadedImageUrl = await PortfolioImageStorage.UploadAsync(
                    _mediaService,
                    request.Image,
                    profile.FreelancerProfilesId,
                    cancellationToken);
            }

            var item = new PortfolioItem
            {
                PortfolioItemsId = Guid.NewGuid(),
                FreelancerId = profile.FreelancerProfilesId,
                Title = request.Dto.Title.Trim(),
                Description = PortfolioItemMapping.NormalizeOptional(request.Dto.Description),
                ProjectUrl = PortfolioItemMapping.NormalizeOptional(request.Dto.ProjectUrl),
                ImageUrl = uploadedImageUrl,
                ProjectDate = request.Dto.ProjectDate
            };

            _context.Set<PortfolioItem>().Add(item);
            await _context.SaveChangesAsync(cancellationToken);

            return item.ToDto();
        }
        catch
        {
            await PortfolioImageStorage.TryDeleteAsync(_mediaService, uploadedImageUrl, _logger);
            throw;
        }
    }
}
