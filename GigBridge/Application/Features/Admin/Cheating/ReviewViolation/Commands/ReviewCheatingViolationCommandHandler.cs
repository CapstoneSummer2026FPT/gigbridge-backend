using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Admin.Cheating.DTOs;
using Application.Features.Admin.Cheating.GetViolations.Queries;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Cheating.ReviewViolation.Commands;

public class ReviewCheatingViolationCommandHandler
    : IRequestHandler<ReviewCheatingViolationCommand, AdminCheatingViolationDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public ReviewCheatingViolationCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<AdminCheatingViolationDto> Handle(
        ReviewCheatingViolationCommand request,
        CancellationToken cancellationToken)
    {
        var violation = await _context.Set<FreelancerCheatingViolation>()
            .Include(existingViolation => existingViolation.FreelancerUser)
            .Include(existingViolation => existingViolation.ReviewedByAdmin)
            .Include(existingViolation => existingViolation.Proposals)
                .ThenInclude(proposal => proposal.JobPosts)
            .FirstOrDefaultAsync(
                existingViolation => existingViolation.FreelancerCheatingViolationsId == request.ViolationId,
                cancellationToken);

        if (violation is null)
        {
            throw new NotFoundException("Cheating violation does not exist.");
        }

        violation.IsReviewed = request.Request.IsReviewed;
        violation.AdminNote = string.IsNullOrWhiteSpace(request.Request.AdminNote)
            ? null
            : request.Request.AdminNote.Trim();
        violation.ReviewedByAdminId = request.Request.IsReviewed ? request.AdminUserId : null;
        violation.ReviewedAt = request.Request.IsReviewed ? _dateTimeService.UtcNow : null;

        await _context.SaveChangesAsync(cancellationToken);

        if (violation.ReviewedByAdminId.HasValue)
        {
            violation.ReviewedByAdmin = await _context.Set<User>()
                .AsNoTracking()
                .FirstOrDefaultAsync(user => user.UserId == violation.ReviewedByAdminId.Value, cancellationToken);
        }

        return GetAdminCheatingViolationsQueryHandler.ToDto(violation);
    }
}
