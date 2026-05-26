using Application.DTOs.Admin;
using Application.Features.Admin.JobPosts.Dto;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Entities;
using Infrastructure.Services.Admin.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Admin;

public sealed class AdminJobPostService : AdminServiceBase, IAdminJobPostService
{
    private readonly IMapper _mapper;

    public AdminJobPostService(IApplicationDbContext dbContext, IMapper mapper) : base(dbContext)
    {
        _mapper = mapper;
    }

    public async Task<PagedResultDto<JobPostDto>> GetAllAsync(PagedQueryDto query, CancellationToken cancellationToken)
    {
        ValidatePaging(query);
        var jobs = DbContext.Set<JobPost>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            jobs = jobs.Where(x => x.Title.ToLower().Contains(search) || x.Description.ToLower().Contains(search));
        }

        if (query.Status.HasValue)
        {
            jobs = jobs.Where(x => x.Status == query.Status);
        }

        jobs = FilterDates(jobs, query, x => x.CreatedAt);
        return await ToPageAsync(jobs.OrderByDescending(x => x.CreatedAt)
            .ProjectTo<JobPostDto>(_mapper.ConfigurationProvider), query, cancellationToken);
    }

    public async Task<JobPostDetailDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await DbContext.Set<JobPost>().AsNoTracking().Where(x => x.JobPostsId == id)
            .ProjectTo<JobPostDetailDto>(_mapper.ConfigurationProvider).SingleOrDefaultAsync(cancellationToken);
        return result ?? throw new NotFoundException(nameof(JobPost), id);
    }

    public async Task<JobPostDetailDto> CancelAsync(Guid id, JobStatusRequestDto request, AdminActorDto actor, CancellationToken cancellationToken)
    {
        if (request.Status != 4)
        {
            throw Invalid("status", "Only administrative cancellation (status 4) is supported.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw Invalid("reason", "A reason is required when cancelling a job post.");
        }

        var job = await DbContext.Set<JobPost>().SingleOrDefaultAsync(x => x.JobPostsId == id, cancellationToken)
            ?? throw new NotFoundException(nameof(JobPost), id);
        var oldValues = new { job.Status, job.UpdatedAt };
        job.Status = 4;
        job.UpdatedAt = DateTime.UtcNow;
        AddAudit(actor, "JobPostCancelled", id, "JobPost", oldValues,
            new { job.Status, job.UpdatedAt, Reason = request.Reason.Trim() });
        await DbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }
}

