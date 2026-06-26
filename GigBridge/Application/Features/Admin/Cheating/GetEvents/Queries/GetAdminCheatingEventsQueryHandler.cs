using Application.Common.Interfaces;
using Application.Features.Admin.Cheating.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Cheating.GetEvents.Queries;

public class GetAdminCheatingEventsQueryHandler
    : IRequestHandler<GetAdminCheatingEventsQuery, AdminCheatingEventsResponse>
{
    private readonly IApplicationDbContext _context;

    public GetAdminCheatingEventsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminCheatingEventsResponse> Handle(
        GetAdminCheatingEventsQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var query = ApplyFilters(_context.Set<ProposalCheatingEvent>().AsNoTracking(), request);
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(cheatingEvent => cheatingEvent.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(cheatingEvent => new AdminCheatingEventDto
            {
                ProposalCheatingEventId = cheatingEvent.ProposalCheatingEventsId,
                ProposalId = cheatingEvent.ProposalsId,
                FreelancerUserId = cheatingEvent.FreelancerUserId,
                FreelancerName = cheatingEvent.FreelancerUser.FullName,
                FreelancerEmail = cheatingEvent.FreelancerUser.Email,
                JobPostId = cheatingEvent.Proposals.JobPostsId,
                JobTitle = cheatingEvent.Proposals.JobPosts.Title,
                JobPostQuestionId = cheatingEvent.JobPostQuestionsId,
                EventType = cheatingEvent.EventType,
                ClientEventId = cheatingEvent.ClientEventId,
                IpAddress = cheatingEvent.IpAddress,
                UserAgent = cheatingEvent.UserAgent,
                Metadata = cheatingEvent.Metadata,
                OccurredAt = cheatingEvent.OccurredAt,
                CreatedAt = cheatingEvent.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new AdminCheatingEventsResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total
        };
    }

    private static IQueryable<ProposalCheatingEvent> ApplyFilters(
        IQueryable<ProposalCheatingEvent> query,
        GetAdminCheatingEventsQuery request)
    {
        if (request.EventType.HasValue)
        {
            query = query.Where(cheatingEvent => cheatingEvent.EventType == request.EventType.Value);
        }

        if (request.FreelancerUserId.HasValue)
        {
            query = query.Where(cheatingEvent => cheatingEvent.FreelancerUserId == request.FreelancerUserId.Value);
        }

        if (request.ProposalId.HasValue)
        {
            query = query.Where(cheatingEvent => cheatingEvent.ProposalsId == request.ProposalId.Value);
        }

        if (request.From.HasValue)
        {
            query = query.Where(cheatingEvent => cheatingEvent.CreatedAt >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(cheatingEvent => cheatingEvent.CreatedAt <= request.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim().ToLower();
            query = query.Where(cheatingEvent =>
                cheatingEvent.FreelancerUser.FullName.ToLower().Contains(keyword) ||
                cheatingEvent.FreelancerUser.Email.ToLower().Contains(keyword) ||
                cheatingEvent.Proposals.JobPosts.Title.ToLower().Contains(keyword));
        }

        return query;
    }
}
