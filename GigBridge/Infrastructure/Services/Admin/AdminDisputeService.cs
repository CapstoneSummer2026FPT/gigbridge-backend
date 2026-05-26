using Application.DTOs.Admin;
using Application.Features.Admin.Disputes.Dto;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Entities;
using Infrastructure.Services.Admin.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Admin;

public sealed class AdminDisputeService : AdminServiceBase, IAdminDisputeService
{
    private readonly IMapper _mapper;

    public AdminDisputeService(IApplicationDbContext dbContext, IMapper mapper) : base(dbContext)
    {
        _mapper = mapper;
    }

    public async Task<PagedResultDto<DisputeDto>> GetAllAsync(PagedQueryDto query, CancellationToken cancellationToken)
    {
        ValidatePaging(query);
        var disputes = DbContext.Set<Dispute>().AsNoTracking();
        if (query.Status.HasValue)
        {
            disputes = disputes.Where(x => x.Status == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            disputes = disputes.Where(x => x.Reason.ToLower().Contains(search));
        }

        disputes = FilterDates(disputes, query, x => x.CreatedAt);
        return await ToPageAsync(disputes.OrderByDescending(x => x.CreatedAt)
            .ProjectTo<DisputeDto>(_mapper.ConfigurationProvider), query, cancellationToken);
    }

    public async Task<DisputeDetailDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var dispute = await DbContext.Set<Dispute>().AsNoTracking()
            .Include(x => x.Contracts)
            .Include(x => x.Milestones).ThenInclude(x => x!.PaymentProofs)
            .Include(x => x.DisputeEvidences)
            .Include(x => x.DisputeMessages)
            .SingleOrDefaultAsync(x => x.DisputesId == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Dispute), id);
        return _mapper.Map<DisputeDetailDto>(dispute);
    }

    public async Task<DisputeDetailDto> ReviewAsync(Guid id, DisputeReviewRequestDto request, AdminActorDto actor, CancellationToken cancellationToken)
    {
        var dispute = await DbContext.Set<Dispute>().SingleOrDefaultAsync(x => x.DisputesId == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Dispute), id);
        if (dispute.Status != 0)
        {
            throw Invalid("status", "Only an open dispute can be placed under review.");
        }

        var oldValues = new { dispute.Status, dispute.UpdatedAt };
        dispute.Status = 1;
        dispute.UpdatedAt = DateTime.UtcNow;
        AddAudit(actor, "DisputeReviewStarted", id, "Dispute", oldValues,
            new { dispute.Status, dispute.UpdatedAt, request.AdminNote });
        await DbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<DisputeDetailDto> ResolveAsync(Guid id, DisputeResolutionRequestDto request, AdminActorDto actor, CancellationToken cancellationToken)
    {
        if (request.Resolution is < 0 or > 3)
        {
            throw Invalid("resolution", "Resolution must be between 0 and 3.");
        }

        if (string.IsNullOrWhiteSpace(request.ResolutionNote))
        {
            throw Invalid("resolutionNote", "A resolution note is required.");
        }

        var dispute = await DbContext.Set<Dispute>()
            .Include(x => x.Contracts).ThenInclude(x => x.ClientProfiles)
            .Include(x => x.Contracts).ThenInclude(x => x.FreelancerProfiles)
            .SingleOrDefaultAsync(x => x.DisputesId == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Dispute), id);
        var oldValues = new { dispute.Status, dispute.Resolution, dispute.ResolutionNote, dispute.ResolvedByAdminId, dispute.ResolvedAt };
        dispute.Status = 2;
        dispute.Resolution = request.Resolution;
        dispute.ResolutionNote = request.ResolutionNote.Trim();
        dispute.ResolvedByAdminId = actor.AdminId;
        dispute.ResolvedAt = DateTime.UtcNow;
        dispute.UpdatedAt = dispute.ResolvedAt;

        var recipients = new[] { dispute.Contracts.ClientProfiles.UserId, dispute.Contracts.FreelancerProfiles.UserId }.Distinct();
        foreach (var userId in recipients)
        {
            DbContext.Set<Domain.Entities.Notification>().Add(new Domain.Entities.Notification
            {
                NotificationsId = Guid.NewGuid(),
                UserId = userId,
                Type = 10,
                Title = "Dispute resolution completed",
                Content = "The administration team has completed its review.",
                ReferenceId = id,
                ReferenceType = "Dispute",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        AddAudit(actor, "DisputeResolved", id, "Dispute", oldValues,
            new { dispute.Status, dispute.Resolution, dispute.ResolutionNote, dispute.ResolvedByAdminId, dispute.ResolvedAt });
        await DbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

}

