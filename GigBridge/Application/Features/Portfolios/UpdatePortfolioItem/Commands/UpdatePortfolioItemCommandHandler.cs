using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Portfolios.Common;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Portfolios.UpdatePortfolioItem.Commands;

public sealed class UpdatePortfolioItemCommandHandler
    : IRequestHandler<UpdatePortfolioItemCommand, PortfolioItemDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IMediaService _mediaService;
    private readonly ILogger<UpdatePortfolioItemCommandHandler> _logger;

    public UpdatePortfolioItemCommandHandler(
        IApplicationDbContext context,
        IMediaService mediaService,
        ILogger<UpdatePortfolioItemCommandHandler> logger)
    {
        _context = context;
        _mediaService = mediaService;
        _logger = logger;
    }

    public async Task<PortfolioItemDto> Handle(
        UpdatePortfolioItemCommand request,
        CancellationToken cancellationToken)
    {
        var item = await _context.Set<PortfolioItem>()
            .Include(portfolioItem => portfolioItem.Freelancer)
            .FirstOrDefaultAsync(
                portfolioItem => portfolioItem.PortfolioItemsId == request.PortfolioItemId &&
                    portfolioItem.Freelancer.UserId == request.UserId,
                cancellationToken);
        if (item is null)
        {
            throw new NotFoundException(nameof(PortfolioItem), request.PortfolioItemId);
        }

        var previousImageUrl = item.ImageUrl;
        string? uploadedImageUrl = null;
        try
        {
            if (request.Image is not null)
            {
                uploadedImageUrl = await PortfolioImageStorage.UploadAsync(
                    _mediaService,
                    request.Image,
                    item.FreelancerId,
                    cancellationToken);
            }

            item.Title = request.Dto.Title.Trim();
            item.Description = PortfolioItemMapping.NormalizeOptional(request.Dto.Description);
            item.ProjectUrl = PortfolioItemMapping.NormalizeOptional(request.Dto.ProjectUrl);
            item.ProjectDate = request.Dto.ProjectDate;

            if (uploadedImageUrl is not null)
            {
                item.ImageUrl = uploadedImageUrl;
            }
            else if (request.RemoveImage || !request.PreserveExistingImage)
            {
                item.ImageUrl = null;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await PortfolioImageStorage.TryDeleteAsync(_mediaService, uploadedImageUrl, _logger);
            throw;
        }

        if (!string.Equals(previousImageUrl, item.ImageUrl, StringComparison.Ordinal))
        {
            await PortfolioImageStorage.TryDeleteAsync(_mediaService, previousImageUrl, _logger);
        }
        return item.ToDto();
    }
}
