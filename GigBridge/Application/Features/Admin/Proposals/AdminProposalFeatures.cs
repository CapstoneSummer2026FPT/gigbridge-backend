using Domain.Enums.Proposals;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Admin.AuditLogs.Interfaces;
using Application.Common.InternalServices.Admin.AuditLogs.Services;
using Application.Common.Models;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.AiInterviews;
using Domain.Enums.Contracts;
using Domain.Enums.Subscriptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Proposals;

public static class ProposalModerationLock
{
    private const long Namespace = 0x50524F504D4F444C;
    public static long For(Guid proposalId) => BitConverter.ToInt64(proposalId.ToByteArray(), 0) ^ Namespace;
}

public sealed record AdminProposalListQuery(string? Search = null, int? LifecycleStatus = null,
    int? ModerationStatus = null, Guid? ClientId = null, Guid? FreelancerId = null, Guid? JobPostId = null,
    DateTime? SubmittedFrom = null, DateTime? SubmittedTo = null, DateTime? UpdatedFrom = null, DateTime? UpdatedTo = null,
    decimal? MinBudget = null, decimal? MaxBudget = null, bool? HasContract = null, int? ContractStatus = null,
    bool? HasReport = null, bool? HasDispute = null, int? AiInterviewStatus = null, int? NegotiationStatus = null,
    string SortBy = "submittedAt", bool SortDescending = true, int Page = 1, int PageSize = 20, int? PageIndex = null)
    : IRequest<PaginatedList<AdminProposalListItem>>;

public sealed record AdminProposalListItem(Guid ProposalId, Guid JobPostId, string JobPostTitle,
    Guid ClientId, string ClientName, string? ClientAvatar, Guid FreelancerId, string FreelancerName, string? FreelancerAvatar, decimal? ProposedBudget,
    string? EstimatedDuration, DateTime? SubmittedAt, DateTime? UpdatedAt, int LifecycleStatus,
    int ModerationStatus, int? AiInterviewStatus, int? NegotiationStatus, bool HasContract,
    Guid? ContractId, int? ContractStatus, bool HasReport, bool HasDispute, int ReportCount, int DisputeCount);

public sealed record AdminProposalParty(Guid UserId, string Name, string? Avatar, string? Summary, int AccountStatus,
    bool IsActive, bool IsFlagged, int ViolationCount, int ReportCount, IReadOnlyList<string> Skills,
    int? EloPoints, bool IsPremium);
public sealed record AdminProposalQuestion(Guid QuestionId, string Question, int Order, bool Required,
    string? Answer, DateTime? AnsweredAt, DateTime? TimerStartedAt, DateTime? TimerCompletedAt, bool? TimerLocked);
public sealed record AdminProposalWorkItem(Guid WorkItemId, string Title, string? Description, string? Deliverables,
    string? EstimatedDuration, int Order);
public sealed record AdminProposalMilestone(Guid MilestoneId, string Title, string? Description, decimal Amount,
    string? EstimatedDuration, DateOnly? DueDate, string? Deliverables, string? AcceptanceCriteria, int Order,
    IReadOnlyList<AdminProposalWorkItem> WorkItems);
public sealed record AdminProposalAiAnswer(int QuestionIndex, string? Question, string? Transcript, int? Score);
public sealed record AdminProposalAi(Guid? DefinitionId, int? DefinitionStatus, Guid? AttemptId, int? AttemptStatus,
    DateTime? StartedAt, DateTime? CompletedAt, int? Score, int? CompatibilityScore, string? Result,
    bool? RecommendedHire, IReadOnlyList<AdminProposalAiAnswer> Answers, int? JudgingScore,
    string? JudgingSummary, DateTime? JudgedAt, DateTime? ReviewStartedAt, DateTime? ReviewCompletedAt, bool? ReviewLocked);
public sealed record AdminProposalNegotiationMilestone(string Title, string? Description, decimal Amount,
    string? EstimatedDuration, DateOnly? DueDate, IReadOnlyList<AdminProposalWorkItem> WorkItems);
public sealed record AdminProposalNegotiationOffer(Guid OfferId, Guid ConversationId, Guid CreatedByUserId,
    string CreatedByName, string? CreatedByAvatar, decimal Budget, DateOnly? StartDate, DateOnly? EndDate, string? Scope,
    int Status, DateTime CreatedAt, DateTime? RespondedAt, IReadOnlyList<AdminProposalNegotiationMilestone> Milestones);
public sealed record AdminProposalContract(Guid ContractId, string Title, int Status, decimal Budget,
    DateOnly? StartDate, DateOnly? EndDate, DateTime CreatedAt, int MilestoneCount, decimal? EscrowFunded,
    decimal? EscrowReleased, int ContractReportCount, int DisputeCount);
public sealed record AdminProposalRelation(Guid Id, string Kind, string Relation, int Status, string? Reason,
    DateTime CreatedAt, Guid? ContractId, Guid? RelatedId);
public sealed record AdminProposalNote(Guid NoteId, Guid AdminId, string AdminName, string? AdminAvatar, string Content, DateTime CreatedAt);
public sealed record AdminProposalAudit(Guid AuditId, Guid AdminId, string AdminName, string? AdminAvatar, string Action,
    string? OldValues, string? NewValues, Guid CorrelationId, DateTime CreatedAt);
public sealed record AdminProposalDetail(Guid ProposalId, string? CoverLetter, decimal? ProposedBudget,
    string? EstimatedDuration, string? AnalysisSummary, string? SolutionApproach, string? Deliverables,
    string? Assumptions, string? OutOfScope, DateTime? SubmittedAt, DateTime? UpdatedAt, int LifecycleStatus,
    int ModerationStatus, string? InvalidationReason, DateTime? InvalidatedAt, Guid? InvalidatedByAdminId,
    string? InvalidatedByAdminName, Guid JobPostId, string JobPostTitle, string JobPostDescription,
    decimal? JobBudgetMin, decimal? JobBudgetMax, string? JobDuration, int JobStatus, int? JobVisibility,
    IReadOnlyList<string> RequiredSkills, AdminProposalParty Client, AdminProposalParty Freelancer,
    IReadOnlyList<AdminProposalQuestion> Answers, IReadOnlyList<AdminProposalMilestone> Milestones,
    IReadOnlyList<AdminProposalWorkItem> UnassignedWorkItems, AdminProposalAi? AiInterview,
    IReadOnlyList<AdminProposalNegotiationOffer> NegotiationHistory, AdminProposalContract? Contract,
    IReadOnlyList<AdminProposalRelation> Reports, IReadOnlyList<AdminProposalRelation> ContractReports,
    IReadOnlyList<AdminProposalRelation> Disputes, IReadOnlyList<AdminProposalNote> InternalNotes,
    IReadOnlyList<AdminProposalAudit> AuditHistory);
public sealed record GetAdminProposalAggregateQuery(Guid ProposalId) : IRequest<AdminProposalDetail>;

public sealed class AdminProposalQueryHandler :
    IRequestHandler<AdminProposalListQuery, PaginatedList<AdminProposalListItem>>,
    IRequestHandler<GetAdminProposalAggregateQuery, AdminProposalDetail>
{
    private readonly IApplicationDbContext _context;
    public AdminProposalQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PaginatedList<AdminProposalListItem>> Handle(AdminProposalListQuery q, CancellationToken ct)
    {
        if (q.LifecycleStatus is < 0 or > 5) throw new BadRequestException("Lifecycle status is invalid.");
        if (q.ModerationStatus is < 0 or > 1) throw new BadRequestException("Moderation status is invalid.");
        if (q.MinBudget < 0 || q.MaxBudget < 0 || (q.MinBudget.HasValue && q.MaxBudget.HasValue && q.MinBudget > q.MaxBudget)) throw new BadRequestException("Budget range is invalid.");
        if (q.SubmittedFrom > q.SubmittedTo || q.UpdatedFrom > q.UpdatedTo) throw new BadRequestException("Date range is invalid.");
        var page = Math.Max(1, q.PageIndex ?? q.Page);
        var size = Math.Clamp(q.PageSize, 1, PaginatedQuery.MaxPageSize);
        var query = _context.Set<Proposal>().AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            var guid = Guid.TryParse(s, out var id) ? id : Guid.Empty;
            query = query.Where(x => x.ProposalsId == guid || x.JobPosts.Title.ToLower().Contains(s) || x.JobPosts.ClientProfiles.User.FullName.ToLower().Contains(s) || x.FreelancerProfiles.User.FullName.ToLower().Contains(s));
        }
        if (q.LifecycleStatus.HasValue) query = query.Where(x => x.Status == q.LifecycleStatus);
        if (q.ModerationStatus.HasValue) query = query.Where(x => x.ModerationStatus == q.ModerationStatus);
        if (q.ClientId.HasValue) query = query.Where(x => x.JobPosts.ClientProfiles.UserId == q.ClientId);
        if (q.FreelancerId.HasValue) query = query.Where(x => x.FreelancerProfiles.UserId == q.FreelancerId);
        if (q.JobPostId.HasValue) query = query.Where(x => x.JobPostsId == q.JobPostId);
        if (q.SubmittedFrom.HasValue) query = query.Where(x => x.SubmittedAt >= q.SubmittedFrom);
        if (q.SubmittedTo.HasValue) query = query.Where(x => x.SubmittedAt <= q.SubmittedTo);
        if (q.UpdatedFrom.HasValue) query = query.Where(x => x.UpdatedAt >= q.UpdatedFrom);
        if (q.UpdatedTo.HasValue) query = query.Where(x => x.UpdatedAt <= q.UpdatedTo);
        if (q.MinBudget.HasValue) query = query.Where(x => x.ProposedBudget >= q.MinBudget);
        if (q.MaxBudget.HasValue) query = query.Where(x => x.ProposedBudget <= q.MaxBudget);
        if (q.HasContract.HasValue) query = q.HasContract.Value ? query.Where(x => x.Contract != null) : query.Where(x => x.Contract == null);
        if (q.ContractStatus.HasValue) query = query.Where(x => x.Contract != null && x.Contract.Status == q.ContractStatus);
        if (q.NegotiationStatus.HasValue) query = query.Where(x => x.NegotiationOffers.Any(o => o.Status == q.NegotiationStatus));
        if (q.AiInterviewStatus.HasValue) query = query.Where(x => _context.Set<AiInterviewAttempt>().Any(a => a.Definition.JobPostId == x.JobPostsId && a.FreelancerUserId == x.FreelancerProfiles.UserId && (int)a.Status == q.AiInterviewStatus));
        if (q.HasDispute.HasValue) query = q.HasDispute.Value ? query.Where(x => x.Contract != null && x.Contract.Disputes.Any()) : query.Where(x => x.Contract == null || !x.Contract.Disputes.Any());
        if (q.HasReport.HasValue) query = q.HasReport.Value ? query.Where(x => (x.Contract != null && x.Contract.ReportContracts.Any()) || _context.Set<Report>().Any(r => (r.ReportedEntityType == "JobPost" && r.ReportedEntityId == x.JobPostsId) || (r.ReportedEntityType == "User" && (r.ReportedEntityId == x.FreelancerProfiles.UserId || r.ReportedEntityId == x.JobPosts.ClientProfiles.UserId)))) : query.Where(x => (x.Contract == null || !x.Contract.ReportContracts.Any()) && !_context.Set<Report>().Any(r => (r.ReportedEntityType == "JobPost" && r.ReportedEntityId == x.JobPostsId) || (r.ReportedEntityType == "User" && (r.ReportedEntityId == x.FreelancerProfiles.UserId || r.ReportedEntityId == x.JobPosts.ClientProfiles.UserId))));
        query = (q.SortBy.ToLowerInvariant(), q.SortDescending) switch { ("updatedat", false) => query.OrderBy(x => x.UpdatedAt), ("updatedat", true) => query.OrderByDescending(x => x.UpdatedAt), ("proposedbudget", false) => query.OrderBy(x => x.ProposedBudget), ("proposedbudget", true) => query.OrderByDescending(x => x.ProposedBudget), ("lifecyclestatus", false) => query.OrderBy(x => x.Status), ("lifecyclestatus", true) => query.OrderByDescending(x => x.Status), ("moderationstatus", false) => query.OrderBy(x => x.ModerationStatus), ("moderationstatus", true) => query.OrderByDescending(x => x.ModerationStatus), (_, false) => query.OrderBy(x => x.SubmittedAt ?? x.UpdatedAt), _ => query.OrderByDescending(x => x.SubmittedAt ?? x.UpdatedAt) };
        var count = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * size).Take(size).Select(x => new AdminProposalListItem(x.ProposalsId, x.JobPostsId, x.JobPosts.Title, x.JobPosts.ClientProfiles.UserId, x.JobPosts.ClientProfiles.User.FullName, x.JobPosts.ClientProfiles.User.Avatar, x.FreelancerProfiles.UserId, x.FreelancerProfiles.User.FullName, x.FreelancerProfiles.User.Avatar, x.ProposedBudget, x.ProposedDuration, x.SubmittedAt, x.UpdatedAt, x.Status, x.ModerationStatus,
            _context.Set<AiInterviewAttempt>().Where(a => a.Definition.JobPostId == x.JobPostsId && a.FreelancerUserId == x.FreelancerProfiles.UserId).OrderByDescending(a => a.StartedAt).Select(a => (int?)a.Status).FirstOrDefault(), x.NegotiationOffers.OrderByDescending(o => o.CreatedAt).Select(o => (int?)o.Status).FirstOrDefault(), x.Contract != null, x.Contract != null ? (Guid?)x.Contract.ContractsId : null, x.Contract != null ? (int?)x.Contract.Status : null,
            (x.Contract != null && x.Contract.ReportContracts.Any()) || _context.Set<Report>().Any(r => (r.ReportedEntityType == "JobPost" && r.ReportedEntityId == x.JobPostsId) || (r.ReportedEntityType == "User" && (r.ReportedEntityId == x.FreelancerProfiles.UserId || r.ReportedEntityId == x.JobPosts.ClientProfiles.UserId))), x.Contract != null && x.Contract.Disputes.Any(), (x.Contract != null ? x.Contract.ReportContracts.Count : 0) + _context.Set<Report>().Count(r => (r.ReportedEntityType == "JobPost" && r.ReportedEntityId == x.JobPostsId) || (r.ReportedEntityType == "User" && (r.ReportedEntityId == x.FreelancerProfiles.UserId || r.ReportedEntityId == x.JobPosts.ClientProfiles.UserId))), x.Contract != null ? x.Contract.Disputes.Count : 0)).ToListAsync(ct);
        return new(items, count, page, size);
    }

    public async Task<AdminProposalDetail> Handle(GetAdminProposalAggregateQuery q, CancellationToken ct)
    {
        var p = await _context.Set<Proposal>().AsNoTracking().Include(x => x.InvalidatedByAdmin)
            .Include(x => x.JobPosts).ThenInclude(x => x.ClientProfiles).ThenInclude(x => x.User)
            .Include(x => x.JobPosts).ThenInclude(x => x.JobPostSkills).ThenInclude(x => x.Skills)
            .Include(x => x.JobPosts).ThenInclude(x => x.JobPostQuestions)
            .Include(x => x.FreelancerProfiles).ThenInclude(x => x.User)
            .Include(x => x.FreelancerProfiles).ThenInclude(x => x.FreelancerSkills).ThenInclude(x => x.Skills)
            .Include(x => x.ProposalAnswers).Include(x => x.ProposalQuestionTimers)
            .Include(x => x.ProposalMilestonePlans).ThenInclude(x => x.WorkItems)
            .Include(x => x.ProposalWorkBreakdownItems).Include(x => x.ProposalAiJudging)
            .Include(x => x.ProposalInterviewReviewSession).Include(x => x.AdminNotes).ThenInclude(x => x.AdminUser)
            .FirstOrDefaultAsync(x => x.ProposalsId == q.ProposalId, ct) ?? throw new NotFoundException("Proposal does not exist.");
        var clientUser = p.JobPosts.ClientProfiles.User;
        var freelancerUser = p.FreelancerProfiles.User;
        var reports = await _context.Set<Report>().AsNoTracking().Where(r => (r.ReportedEntityType == "JobPost" && r.ReportedEntityId == p.JobPostsId) || (r.ReportedEntityType == "User" && (r.ReportedEntityId == clientUser.UserId || r.ReportedEntityId == freelancerUser.UserId))).OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        var offers = await _context.Set<NegotiationOffer>().AsNoTracking().Include(x => x.ClientProfiles).ThenInclude(x => x.User).Include(x => x.FreelancerProfiles).ThenInclude(x => x.User).Include(x => x.NegotiationOfferMilestones).ThenInclude(x => x.WorkItems).Where(x => x.ProposalsId == p.ProposalsId).OrderBy(x => x.CreatedAt).ToListAsync(ct);
        var contract = await _context.Set<Contract>().AsNoTracking().Include(x => x.ContractEscrow).Include(x => x.Milestones).Include(x => x.ReportContracts).Include(x => x.Disputes).FirstOrDefaultAsync(x => x.ProposalsId == p.ProposalsId, ct);
        var contractReports = contract is null ? [] : contract.ReportContracts.OrderByDescending(x => x.CreatedAt).Select(x => new AdminProposalRelation(x.ReportContractId, "ContractReport", "IndirectThroughContract", x.AdminReviewStatus, x.Description, x.CreatedAt, x.ContractId, null)).ToList();
        var disputes = contract is null ? [] : contract.Disputes.OrderByDescending(x => x.CreatedAt).Select(x => new AdminProposalRelation(x.DisputesId, "Dispute", "IndirectThroughContract", x.Status, x.Reason, x.CreatedAt, x.ContractsId, x.RelatedReportId)).ToList();
        var definition = await _context.Set<AiInterviewDefinition>().AsNoTracking().Include(x => x.Attempts.Where(a => a.FreelancerUserId == freelancerUser.UserId)).ThenInclude(x => x.Answers).Where(x => x.JobPostId == p.JobPostsId).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        var attempt = definition?.Attempts.OrderByDescending(x => x.StartedAt).FirstOrDefault();
        var audits = await _context.Set<AdminAuditLog>().AsNoTracking().Where(x => x.EntityType == nameof(Proposal) && x.EntityId == p.ProposalsId).OrderByDescending(x => x.CreatedAt).Select(x => new AdminProposalAudit(x.AdminAuditLogsId, x.AdminId, x.Admin.FullName, x.Admin.Avatar, x.Action, x.OldValues, x.NewValues, x.CorrelationId, x.CreatedAt)).ToListAsync(ct);
        var reportCounts = await _context.Set<Report>().AsNoTracking().Where(x => x.ReportedEntityType == "User" && (x.ReportedEntityId == clientUser.UserId || x.ReportedEntityId == freelancerUser.UserId)).GroupBy(x => x.ReportedEntityId).Select(x => new { Id = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        async Task<AdminProposalParty> Party(User u, string? summary, IReadOnlyList<string> skills, int? elo) => new(u.UserId, u.FullName, u.Avatar, summary, u.AccountStatus, u.IsActive, u.IsFlagged, u.ViolationCount, reportCounts.GetValueOrDefault(u.UserId), skills, elo, await _context.Set<Subscription>().AnyAsync(s => s.UserId == u.UserId && s.Status == SubscriptionStatus.Active && s.EndDate > DateTime.UtcNow, ct));
        var freelancerElo = await _context.Set<UserEloScore>().AsNoTracking().Where(x => x.UserId == freelancerUser.UserId).Select(x => (int?)x.CurrentPoints).FirstOrDefaultAsync(ct);
        var answers = p.JobPosts.JobPostQuestions.OrderBy(x => x.OrderIndex).Select(question =>
        {
            var a = p.ProposalAnswers.FirstOrDefault(x => x.JobPostQuestionsId == question.JobPostQuestionsId);
            var t = p.ProposalQuestionTimers.FirstOrDefault(x => x.JobPostQuestionsId == question.JobPostQuestionsId);
            return new AdminProposalQuestion(question.JobPostQuestionsId, question.QuestionText, question.OrderIndex, question.IsRequired, a?.AnswerText, a?.UpdatedAt ?? a?.CreatedAt, t?.StartedAt, t?.CompletedAt, t?.IsLocked);
        }).ToList();
        var milestones = p.ProposalMilestonePlans.OrderBy(x => x.OrderIndex).Select(x => new AdminProposalMilestone(x.ProposalMilestonePlansId, x.Title, x.Description, x.Amount, x.EstimatedDuration, x.DueDate, x.Deliverables, x.AcceptanceCriteria, x.OrderIndex, x.WorkItems.OrderBy(w => w.OrderIndex).Select(Work).ToList())).ToList();
        AdminProposalAi? ai = definition is null && p.ProposalAiJudging is null ? null : new(definition?.AiInterviewDefinitionsId, definition is null ? null : (int)definition.Status, attempt?.AiInterviewAttemptsId, attempt is null ? null : (int)attempt.Status, attempt?.StartedAt, attempt?.CompletedAt, attempt?.OverallScore, attempt?.CompatibilityScore, attempt?.EvaluationSummary, attempt?.RecommendedHire, attempt?.Answers.OrderBy(x => x.QuestionIndex).Select(x => new AdminProposalAiAnswer(x.QuestionIndex, x.QuestionText, x.Transcript, x.Score)).ToList() ?? [], p.ProposalAiJudging?.Score, p.ProposalAiJudging?.Summary, p.ProposalAiJudging?.EvaluatedAt, p.ProposalInterviewReviewSession?.StartedAt, p.ProposalInterviewReviewSession?.CompletedAt, p.ProposalInterviewReviewSession?.IsLocked);
        var offerDtos = offers.Select(o => new AdminProposalNegotiationOffer(o.NegotiationOfferId, o.ConversationsId, o.Status == 0 ? o.ClientProfiles.UserId : o.FreelancerProfiles.UserId, o.Status == 0 ? o.ClientProfiles.User.FullName : o.FreelancerProfiles.User.FullName, o.Status == 0 ? o.ClientProfiles.User.Avatar : o.FreelancerProfiles.User.Avatar, o.FinalPrice, o.StartDate, o.EndDate, o.ScopeSummary, o.Status, o.CreatedAt, o.RespondedAt, o.NegotiationOfferMilestones.OrderBy(m => m.OrderIndex).Select(m => new AdminProposalNegotiationMilestone(m.Title, m.Description, m.Amount, m.EstimatedDuration, m.DueDate, m.WorkItems.OrderBy(w => w.OrderIndex).Select(w => new AdminProposalWorkItem(w.NegotiationOfferWorkItemId, w.Title, w.Description, w.Deliverables, w.EstimatedDuration, w.OrderIndex)).ToList())).ToList())).ToList();
        return new(p.ProposalsId, p.CoverLetter, p.ProposedBudget, p.ProposedDuration, p.AnalysisSummary, p.SolutionApproach, p.Deliverables, p.Assumptions, p.OutOfScope, p.SubmittedAt, p.UpdatedAt, p.Status, p.ModerationStatus, p.InvalidationReason, p.InvalidatedAt, p.InvalidatedByAdminId, p.InvalidatedByAdmin?.FullName, p.JobPostsId, p.JobPosts.Title, p.JobPosts.Description, p.JobPosts.BudgetMin, p.JobPosts.BudgetMax, p.JobPosts.EstimatedDuration, p.JobPosts.Status, p.JobPosts.Visibility, p.JobPosts.JobPostSkills.Select(x => x.Skills.Name).Concat(p.JobPosts.CustomSkillNames).Distinct().ToList(), await Party(clientUser, p.JobPosts.ClientProfiles.CompanyName ?? p.JobPosts.ClientProfiles.CompanyDescription, [], null), await Party(freelancerUser, p.FreelancerProfiles.Title ?? p.FreelancerProfiles.Bio, p.FreelancerProfiles.FreelancerSkills.Select(x => x.Skills.Name).ToList(), freelancerElo), answers, milestones, p.ProposalWorkBreakdownItems.Where(x => x.ProposalMilestonePlansId == null).OrderBy(x => x.OrderIndex).Select(Work).ToList(), ai, offerDtos,
            contract is null ? null : new(contract.ContractsId, contract.Title, contract.Status, contract.TotalBudget, contract.StartDate, contract.EndDate, contract.CreatedAt, contract.Milestones.Count, contract.ContractEscrow?.FundedAmount, contract.ContractEscrow?.ReleasedAmount, contract.ReportContracts.Count, contract.Disputes.Count), reports.Select(r => new AdminProposalRelation(r.ReportsId, "Report", r.ReportedEntityType == "JobPost" ? "IndirectThroughJobPost" : r.ReportedEntityId == clientUser.UserId ? "IndirectThroughClient" : "IndirectThroughFreelancer", r.Status, r.Reason, r.CreatedAt, null, r.ReportedEntityId)).ToList(), contractReports, disputes, p.AdminNotes.Where(x => x.IsActive).OrderBy(x => x.CreatedAt).Select(x => new AdminProposalNote(x.ProposalAdminNoteId, x.AdminUserId, x.AdminUser.FullName, x.AdminUser.Avatar, x.Content, x.CreatedAt)).ToList(), audits);
    }
    private static AdminProposalWorkItem Work(ProposalWorkBreakdownItem x) => new(x.ProposalWorkBreakdownItemsId, x.Title, x.Description, x.Deliverables, x.EstimatedDuration, x.OrderIndex);
}

public sealed record ProposalModerationRequest(string Reason, string? InternalNote);
public sealed record InvalidateProposalCommand(Guid AdminId, Guid ProposalId, ProposalModerationRequest Request) : IRequest<AdminProposalDetail>;
public sealed record RestoreProposalCommand(Guid AdminId, Guid ProposalId, ProposalModerationRequest Request) : IRequest<AdminProposalDetail>;
public sealed record AddProposalAdminNoteRequest(string Content);
public sealed record AddProposalAdminNoteCommand(Guid AdminId, Guid ProposalId, string Content) : IRequest<AdminProposalDetail>;

public sealed class AdminProposalMutationHandler : IRequestHandler<InvalidateProposalCommand, AdminProposalDetail>, IRequestHandler<RestoreProposalCommand, AdminProposalDetail>, IRequestHandler<AddProposalAdminNoteCommand, AdminProposalDetail>
{
    private readonly IApplicationDbContext _context;
    private readonly IAdminAuditService _audit;
    private readonly IDateTimeService _clock;
    private readonly IMediator _mediator;

    public AdminProposalMutationHandler(IApplicationDbContext context, IAdminAuditService audit, IDateTimeService clock, IMediator mediator)
    {
        _context = context;
        _audit = audit;
        _clock = clock;
        _mediator = mediator;
    }
    public async Task<AdminProposalDetail> Handle(InvalidateProposalCommand q, CancellationToken ct) => await Change(q.AdminId, q.ProposalId, true, q.Request.Reason, q.Request.InternalNote, ct);
    public async Task<AdminProposalDetail> Handle(RestoreProposalCommand q, CancellationToken ct) => await Change(q.AdminId, q.ProposalId, false, q.Request.Reason, q.Request.InternalNote, ct);
    public async Task<AdminProposalDetail> Handle(AddProposalAdminNoteCommand q, CancellationToken ct)
    {
        var content = Required(q.Content, "Internal note", 5000);
        await using var tx = await _context.BeginTransactionAsync(ct);
        await tx.AcquireTransactionLockAsync(ProposalModerationLock.For(q.ProposalId), ct);
        if (!await _context.Set<Proposal>().AnyAsync(x => x.ProposalsId == q.ProposalId, ct)) throw new NotFoundException("Proposal does not exist.");
        var note = new ProposalAdminNote { ProposalAdminNoteId = Guid.NewGuid(), ProposalId = q.ProposalId, AdminUserId = q.AdminId, Content = content, CreatedAt = _clock.UtcNow, IsActive = true };
        _context.Set<ProposalAdminNote>().Add(note);
        _audit.Add(q.AdminId, AdminAuditActions.ProposalInternalNoteAdded, nameof(Proposal), q.ProposalId, null, new { noteId = note.ProposalAdminNoteId, content });
        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await _mediator.Send(new GetAdminProposalAggregateQuery(q.ProposalId), ct);
    }
    private async Task<AdminProposalDetail> Change(Guid adminId, Guid proposalId, bool invalidate, string reasonValue, string? note, CancellationToken ct)
    {
        var reason = Required(reasonValue, "Reason", 2000);
        await using var tx = await _context.BeginTransactionAsync(ct);
        await tx.AcquireTransactionLockAsync(ProposalModerationLock.For(proposalId), ct);
        var p = await _context.Set<Proposal>().FirstOrDefaultAsync(x => x.ProposalsId == proposalId, ct) ?? throw new NotFoundException("Proposal does not exist.");
        var old = p.ModerationStatus;
        if (invalidate && old == (int)ProposalModerationStatus.Invalidated) throw new ConflictException("Proposal is already invalidated.");
        if (!invalidate && old == (int)ProposalModerationStatus.Active) throw new ConflictException("Proposal is already active.");
        if (invalidate)
        {
            p.ModerationStatus = (int)ProposalModerationStatus.Invalidated;
            p.InvalidatedByAdminId = adminId;
            p.InvalidatedAt = _clock.UtcNow;
            p.InvalidationReason = reason;
        }
        else
        {
            p.ModerationStatus = (int)ProposalModerationStatus.Active;
            p.InvalidatedByAdminId = null;
            p.InvalidatedAt = null; // retain InvalidationReason as latest moderation evidence
        }
        if (!string.IsNullOrWhiteSpace(note)) _context.Set<ProposalAdminNote>().Add(new ProposalAdminNote { ProposalAdminNoteId = Guid.NewGuid(), ProposalId = proposalId, AdminUserId = adminId, Content = Required(note, "Internal note", 5000), CreatedAt = _clock.UtcNow, IsActive = true });
        _audit.Add(adminId, invalidate ? AdminAuditActions.ProposalInvalidated : AdminAuditActions.ProposalRestored, nameof(Proposal), proposalId, new { moderationStatus = old, lifecycleStatus = p.Status }, new { moderationStatus = p.ModerationStatus, lifecycleStatus = p.Status, reason, p.JobPostsId, p.FreelancerProfilesId, contractId = await _context.Set<Contract>().Where(x => x.ProposalsId == proposalId).Select(x => (Guid?)x.ContractsId).FirstOrDefaultAsync(ct) });
        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await _mediator.Send(new GetAdminProposalAggregateQuery(proposalId), ct);
    }

    private static string Required(string? value, string field, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new BadRequestException($"{field} is required.");
        var result = value.Trim();
        if (result.Length > max) throw new BadRequestException($"{field} must not exceed {max} characters.");
        return result;
    }
}
