using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.FAQs.Delete.Commands;

public sealed class DeleteFAQCommandHandler : IRequestHandler<DeleteFAQCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteFAQCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(DeleteFAQCommand request, CancellationToken cancellationToken)
    {
        var faq = await _context.Set<Faq>()
            .FirstOrDefaultAsync(f => f.FaqsId == request.Id, cancellationToken);

        if (faq is null)
            return false;

        _context.Set<Faq>().Remove(faq);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
