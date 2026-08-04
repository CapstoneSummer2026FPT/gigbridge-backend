using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.WorkExperiences.DeleteWorkExperience.Commands;

public sealed class DeleteWorkExperienceCommandHandler
    : IRequestHandler<DeleteWorkExperienceCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteWorkExperienceCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(
        DeleteWorkExperienceCommand request,
        CancellationToken cancellationToken)
    {
        var experience = await _context.Set<WorkExperience>()
            .Include(item => item.Freelancer)
            .FirstOrDefaultAsync(
                item => item.WorkExperiencesId == request.WorkExperienceId &&
                    item.Freelancer.UserId == request.UserId,
                cancellationToken);
        if (experience is null)
        {
            throw new NotFoundException(nameof(WorkExperience), request.WorkExperienceId);
        }

        _context.Set<WorkExperience>().Remove(experience);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
