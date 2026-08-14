using Application.Common.Interfaces;
using Application.Features.JobPosts.Public.GetJobPostDetail.Queries;
using Application.Features.Seo.PublicMarketplace.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Seo.PublicMarketplace.Queries;

public sealed class GetPublicJobPostDetailQueryHandler
    : IRequestHandler<GetPublicJobPostDetailQuery, PublicJobPostDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IMediator _mediator;

    public GetPublicJobPostDetailQueryHandler(IApplicationDbContext context, IMediator mediator)
    {
        _context = context;
        _mediator = mediator;
    }

    public async Task<PublicJobPostDetailDto> Handle(
        GetPublicJobPostDetailQuery request,
        CancellationToken cancellationToken)
    {
        var updatedAt = await _context.Set<Domain.Entities.JobPost>()
            .AsNoTracking()
            .Where(job => job.JobPostsId == request.JobPostId)
            .Select(job => job.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var detail = await _mediator.Send(
            new GetJobPostDetailQuery(request.JobPostId),
            cancellationToken);

        return new PublicJobPostDetailDto(
            detail.JobPostsId,
            detail.ClientProfilesId,
            detail.UserId,
            detail.FullName,
            detail.Avatar,
            detail.ClientFullName,
            detail.Title,
            detail.Description,
            detail.MajorCategoryId,
            detail.MajorId,
            detail.MajorName,
            detail.CategoryId,
            detail.CategoryName,
            detail.BudgetMin,
            detail.BudgetMax,
            detail.Currency,
            detail.EstimatedDuration,
            detail.Location,
            detail.Status,
            detail.Visibility,
            detail.EndDate,
            detail.CreatedAt,
            updatedAt,
            detail.EloPoints,
            detail.Skills,
            detail.CustomSkillNames,
            detail.MilestonePlans,
            detail.HasAiInterview);
    }
}
