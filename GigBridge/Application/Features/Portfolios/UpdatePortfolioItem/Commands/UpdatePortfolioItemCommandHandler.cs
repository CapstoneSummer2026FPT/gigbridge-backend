using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Portfolios.Common;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Portfolios.UpdatePortfolioItem.Commands;

public sealed class UpdatePortfolioItemCommandHandler
    : IRequestHandler<UpdatePortfolioItemCommand, PortfolioItemDto>
{
    private readonly IApplicationDbContext _context;

    public UpdatePortfolioItemCommandHandler(IApplicationDbContext context)
    {
        _context = context;
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

        item.Title = request.Dto.Title.Trim();
        item.Description = PortfolioItemMapping.NormalizeOptional(request.Dto.Description);
        item.ProjectUrl = PortfolioItemMapping.NormalizeOptional(request.Dto.ProjectUrl);
        item.ImageUrl = PortfolioItemMapping.NormalizeOptional(request.Dto.ImageUrl);
        item.ProjectDate = request.Dto.ProjectDate;

        await _context.SaveChangesAsync(cancellationToken);

        return item.ToDto();
    }
}
