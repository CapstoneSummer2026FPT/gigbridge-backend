using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Portfolios.Common;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Portfolios.CreatePortfolioItem.Commands;

public sealed class CreatePortfolioItemCommandHandler
    : IRequestHandler<CreatePortfolioItemCommand, PortfolioItemDto>
{
    private const int MaximumPortfolioItems = 20;
    private readonly IApplicationDbContext _context;

    public CreatePortfolioItemCommandHandler(IApplicationDbContext context)
    {
        _context = context;
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

        var item = new PortfolioItem
        {
            PortfolioItemsId = Guid.NewGuid(),
            FreelancerId = profile.FreelancerProfilesId,
            Title = request.Dto.Title.Trim(),
            Description = PortfolioItemMapping.NormalizeOptional(request.Dto.Description),
            ProjectUrl = PortfolioItemMapping.NormalizeOptional(request.Dto.ProjectUrl),
            ImageUrl = PortfolioItemMapping.NormalizeOptional(request.Dto.ImageUrl),
            ProjectDate = request.Dto.ProjectDate
        };

        _context.Set<PortfolioItem>().Add(item);
        await _context.SaveChangesAsync(cancellationToken);

        return item.ToDto();
    }
}
