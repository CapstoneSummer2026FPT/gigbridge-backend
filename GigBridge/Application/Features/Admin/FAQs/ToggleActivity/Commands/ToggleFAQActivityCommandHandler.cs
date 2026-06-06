using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.FAQs.ToggleActivity.Commands;

public sealed class ToggleFAQActivityCommandHandler : IRequestHandler<ToggleFAQActivityCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public ToggleFAQActivityCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(ToggleFAQActivityCommand request, CancellationToken cancellationToken)
    {
        var faq = await _context.Set<Faq>()
            .FirstOrDefaultAsync(f => f.FaqsId == request.Id, cancellationToken);

        if (faq is null)
            throw new NotFoundException($"FAQ with ID {request.Id} not found.");

        faq.IsActive = !(faq.IsActive ?? true);
        faq.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
