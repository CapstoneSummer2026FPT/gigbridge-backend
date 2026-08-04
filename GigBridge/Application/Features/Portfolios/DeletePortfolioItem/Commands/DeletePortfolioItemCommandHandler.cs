using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Portfolios.DeletePortfolioItem.Commands;

public sealed class DeletePortfolioItemCommandHandler : IRequestHandler<DeletePortfolioItemCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeletePortfolioItemCommandHandler(IApplicationDbContext context)
    {
        _context = context;
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

        return true;
    }
}
