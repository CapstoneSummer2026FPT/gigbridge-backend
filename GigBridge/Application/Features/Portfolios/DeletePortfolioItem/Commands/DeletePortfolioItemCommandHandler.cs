using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Portfolios.Common;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Portfolios.DeletePortfolioItem.Commands;

public sealed class DeletePortfolioItemCommandHandler : IRequestHandler<DeletePortfolioItemCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IMediaService _mediaService;
    private readonly ILogger<DeletePortfolioItemCommandHandler> _logger;

    public DeletePortfolioItemCommandHandler(
        IApplicationDbContext context,
        IMediaService mediaService,
        ILogger<DeletePortfolioItemCommandHandler> logger)
    {
        _context = context;
        _mediaService = mediaService;
        _logger = logger;
    }

    public async Task<bool> Handle(
        DeletePortfolioItemCommand request,
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

        _context.Set<PortfolioItem>().Remove(item);
        await _context.SaveChangesAsync(cancellationToken);
        await PortfolioImageStorage.TryDeleteAsync(_mediaService, item.ImageUrl, _logger);

        return true;
    }
}
