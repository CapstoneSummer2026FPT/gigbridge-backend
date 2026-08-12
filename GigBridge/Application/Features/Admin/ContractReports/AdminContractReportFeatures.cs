using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Features.Admin.AuditLogs.Common.Interfaces;
using Application.Features.Admin.AuditLogs.Common.Services;
using Application.Features.Notifications.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Contracts.Common.Internal;
using Application.Features.ReportContracts.Common.Internal;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Chat;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Escrow;
using Domain.Enums.Disputes;
using Domain.Enums.Notifications;
using Domain.Enums.Reports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.ContractReports;

public sealed record AdminContractReportListItem(
    Guid ReportContractId, Guid ContractId, string ContractTitle, Guid JobPostId, string JobPostTitle,
    Guid ReporterId, string ReporterName, string ReporterRole, Guid? RespondentId, string? RespondentName, string? RespondentRole,
    Guid? MilestoneId, string? MilestoneTitle, int IssueType, int Status, int AdminReviewStatus,
    DateTime CreatedAt, DateTime? UpdatedAt, int? ResolutionAction, int AttachmentCount,
    Guid? AssignedAdminId, string? AssignedAdminName, Guid? RelatedDisputeId, int? DisputeStatus, bool EscalationEligible);

public sealed record GetAdminContractReportsQuery(
    string? Search = null, int? Status = null, int? AdminReviewStatus = null, int? IssueType = null,
    Guid? ReporterId = null, Guid? RespondentId = null, Guid? ClientId = null, Guid? FreelancerId = null,
    Guid? ContractId = null, Guid? JobPostId = null, Guid? MilestoneId = null,
    DateTime? CreatedFrom = null, DateTime? CreatedTo = null, DateTime? UpdatedFrom = null, DateTime? UpdatedTo = null,
    bool? HasAttachments = null, bool? HasResponse = null, Guid? AssignedAdminId = null, bool UnassignedOnly = false,
    bool? HasRelatedDispute = null, bool? Escalated = null, string SortBy = "createdAt", bool SortDescending = true,
    int Page = 1, int PageSize = 20) : IRequest<PaginatedList<AdminContractReportListItem>>;

public sealed record AdminContractReportParty(Guid UserId, string Name, string Email, string Role, int AccountStatus, int ViolationCount, bool IsFlagged);
public sealed record AdminContractReportAttachment(Guid AttachmentId, string FileName, string ContentType, long FileSize, DateTime UploadedAt, Guid? UploadedByUserId, string? UploadedByName, bool CopiedToDispute);
public sealed record AdminContractReportNote(Guid NoteId, Guid AdminUserId, string AdminName, string Content, DateTime CreatedAt, DateTime? UpdatedAt);
public sealed record AdminContractInformationRequest(Guid InformationRequestId, Guid RequestId, Guid TargetUserId, string TargetName, string Message, string? RequestedEvidenceOrClarification, DateTime? DueAt, int Status, DateTime CreatedAt, DateTime? RespondedAt);
public sealed record AdminContractReportMilestone(Guid MilestoneId, string Title, decimal Amount, int Status, DateTime? SubmittedAt, DateTime? ApprovedAt, decimal ReleasedAmount, decimal RefundAmount, decimal PenaltyAmount);
public sealed record AdminContractReportLedger(Guid TransactionId, Guid? MilestoneId, decimal Amount, int Type, int Status, DateTime CreatedAt);
public sealed record AdminContractReportMessage(Guid MessageId, Guid ConversationId, Guid? SenderUserId, string? SenderName, int MessageType, string? Content, DateTime SentAt);
public sealed record AdminContractReportAudit(Guid AuditId, Guid AdminId, string? AdminName, string Action, string? OldValues, string? NewValues, Guid CorrelationId, DateTime CreatedAt);
public sealed record AdminContractReportDetail(
    Guid ReportContractId, int IssueType, string Description, string DesiredResolution, int Status, int AdminReviewStatus,
    DateTime CreatedAt, DateTime? UpdatedAt, int? ResolutionAction, int? AdminResolutionAction, string? AdminResolutionNote,
    Guid? AssignedAdminId, string? AssignedAdminName, DateTime? AssignedAt, Guid? RelatedDisputeId, int? RelatedDisputeStatus,
    AdminContractReportParty Reporter, AdminContractReportParty? Respondent, AdminContractReportParty Client, AdminContractReportParty? Freelancer,
    Guid ContractId, string ContractTitle, int ContractStatus, decimal ContractBudget, DateOnly? StartDate, DateOnly? EndDate,
    Guid JobPostId, string JobPostTitle, Guid? ProposalId, bool ContractLocked, int ContractReportCount, int DisputeCount,
    AdminContractReportMilestone? Milestone, IReadOnlyList<AdminContractReportAttachment> Attachments,
    string? Explanation, string? ProposedResolution, string? RejectReason, DateTime? RespondedAt, DateTime? ResolvedAt,
    decimal EscrowRequired, decimal EscrowFunded, decimal EscrowReleased, decimal EscrowRemaining,
    IReadOnlyList<AdminContractReportLedger> EscrowTransactions, IReadOnlyList<AdminContractReportLedger> WalletTransactions,
    IReadOnlyList<AdminContractReportMessage> Messages, IReadOnlyList<AdminContractReportNote> InternalNotes,
    IReadOnlyList<AdminContractInformationRequest> InformationRequests, IReadOnlyList<AdminContractReportAudit> AuditHistory,
    bool CanAssign, bool CanRequestInformation, bool CanClose, bool CanDismiss, bool CanEscalate, bool CanLinkDispute);

public sealed record GetAdminContractReportDetailQuery(Guid ReportId, Guid? AdminId = null, bool AuditSensitiveAccess = false) : IRequest<AdminContractReportDetail>;
public sealed record GetAdminContractReportAuditQuery(Guid ReportId) : IRequest<IReadOnlyList<AdminContractReportAudit>>;

public sealed class AdminContractReportQueryHandler :
    IRequestHandler<GetAdminContractReportsQuery, PaginatedList<AdminContractReportListItem>>,
    IRequestHandler<GetAdminContractReportDetailQuery, AdminContractReportDetail>,
    IRequestHandler<GetAdminContractReportAuditQuery, IReadOnlyList<AdminContractReportAudit>>
{
    private readonly IApplicationDbContext _context; private readonly IAdminAuditService _audit;
    public AdminContractReportQueryHandler(IApplicationDbContext context, IAdminAuditService audit) { _context = context; _audit = audit; }

    public async Task<PaginatedList<AdminContractReportListItem>> Handle(GetAdminContractReportsQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page); var size = Math.Clamp(q.PageSize, 1, 100);
        var query = _context.Set<ReportContract>().AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Search)) { var s = q.Search.Trim().ToLower(); query = query.Where(x => x.Description.ToLower().Contains(s) || x.Contract.Title.ToLower().Contains(s) || x.Reporter.FullName.ToLower().Contains(s) || (x.Respondent != null && x.Respondent.FullName.ToLower().Contains(s))); }
        if (q.Status.HasValue) query = query.Where(x => x.Status == q.Status);
        if (q.AdminReviewStatus.HasValue) query = query.Where(x => x.AdminReviewStatus == q.AdminReviewStatus);
        if (q.IssueType.HasValue) query = query.Where(x => x.IssueType == q.IssueType);
        if (q.ReporterId.HasValue) query = query.Where(x => x.ReporterId == q.ReporterId);
        if (q.RespondentId.HasValue) query = query.Where(x => x.RespondentId == q.RespondentId);
        if (q.ClientId.HasValue) query = query.Where(x => x.Contract.ClientProfiles.UserId == q.ClientId);
        if (q.FreelancerId.HasValue) query = query.Where(x => x.Contract.FreelancerProfiles != null && x.Contract.FreelancerProfiles.UserId == q.FreelancerId);
        if (q.ContractId.HasValue) query = query.Where(x => x.ContractId == q.ContractId);
        if (q.JobPostId.HasValue) query = query.Where(x => x.Contract.JobPostsId == q.JobPostId);
        if (q.MilestoneId.HasValue) query = query.Where(x => x.MilestoneId == q.MilestoneId);
        if (q.CreatedFrom.HasValue) query = query.Where(x => x.CreatedAt >= q.CreatedFrom);
        if (q.CreatedTo.HasValue) query = query.Where(x => x.CreatedAt <= q.CreatedTo);
        if (q.UpdatedFrom.HasValue) query = query.Where(x => x.UpdatedAt >= q.UpdatedFrom);
        if (q.UpdatedTo.HasValue) query = query.Where(x => x.UpdatedAt <= q.UpdatedTo);
        if (q.HasAttachments.HasValue) query = q.HasAttachments.Value ? query.Where(x => x.ReportContractAttachments.Any()) : query.Where(x => !x.ReportContractAttachments.Any());
        if (q.HasResponse.HasValue) query = q.HasResponse.Value ? query.Where(x => x.RespondedAt != null) : query.Where(x => x.RespondedAt == null);
        if (q.UnassignedOnly) query = query.Where(x => x.AssignedAdminId == null); else if (q.AssignedAdminId.HasValue) query = query.Where(x => x.AssignedAdminId == q.AssignedAdminId);
        if (q.HasRelatedDispute.HasValue) query = q.HasRelatedDispute.Value ? query.Where(x => x.RelatedDisputes.Any()) : query.Where(x => !x.RelatedDisputes.Any());
        if (q.Escalated.HasValue) query = query.Where(x => x.IsEscalatedToDispute == q.Escalated);
        query = (q.SortBy.ToLowerInvariant(), q.SortDescending) switch {
            ("updatedat", false) => query.OrderBy(x => x.UpdatedAt ?? x.CreatedAt), ("updatedat", true) => query.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt),
            ("status", false) => query.OrderBy(x => x.AdminReviewStatus), ("status", true) => query.OrderByDescending(x => x.AdminReviewStatus),
            (_, false) => query.OrderBy(x => x.CreatedAt), _ => query.OrderByDescending(x => x.CreatedAt) };
        var count = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * size).Take(size).Select(x => new AdminContractReportListItem(
            x.ReportContractId, x.ContractId, x.Contract.Title, x.Contract.JobPostsId, x.Contract.JobPosts.Title,
            x.ReporterId, x.Reporter.FullName, x.Contract.ClientProfiles.UserId == x.ReporterId ? "Client" : "Freelancer",
            x.RespondentId, x.Respondent != null ? x.Respondent.FullName : null, x.RespondentId == x.Contract.ClientProfiles.UserId ? "Client" : x.RespondentId != null ? "Freelancer" : null,
            x.MilestoneId, x.Milestone != null ? x.Milestone.Title : null, x.IssueType, x.Status, x.AdminReviewStatus, x.CreatedAt, x.UpdatedAt,
            x.ResolutionAction, x.ReportContractAttachments.Count, x.AssignedAdminId, x.AssignedAdmin != null ? x.AssignedAdmin.FullName : null,
            x.RelatedDisputes.Select(d => (Guid?)d.DisputesId).FirstOrDefault(), x.RelatedDisputes.Select(d => (int?)d.Status).FirstOrDefault(),
            !x.RelatedDisputes.Any() && x.AdminReviewStatus != (int)ContractReportAdminStatus.Closed && x.AdminReviewStatus != (int)ContractReportAdminStatus.Dismissed)).ToListAsync(ct);
        return new(items, count, page, size);
    }

    public async Task<AdminContractReportDetail> Handle(GetAdminContractReportDetailQuery q, CancellationToken ct)
    {
        var r = await _context.Set<ReportContract>().AsNoTracking()
            .Include(x => x.Reporter).Include(x => x.Respondent).Include(x => x.AssignedAdmin)
            .Include(x => x.Contract).ThenInclude(x => x.ClientProfiles).ThenInclude(x => x.User)
            .Include(x => x.Contract).ThenInclude(x => x.FreelancerProfiles).ThenInclude(x => x!.User)
            .Include(x => x.Contract).ThenInclude(x => x.JobPosts)
            .Include(x => x.Milestone).Include(x => x.ReportContractAttachments)
            .Include(x => x.AdminNotes).ThenInclude(x => x.AdminUser)
            .Include(x => x.InformationRequests).ThenInclude(x => x.TargetUser)
            .Include(x => x.RelatedDisputes)
            .FirstOrDefaultAsync(x => x.ReportContractId == q.ReportId, ct) ?? throw new NotFoundException("Contract report does not exist.");
        var c = r.Contract; var related = r.RelatedDisputes.SingleOrDefault();
        var escrow = await _context.Set<ContractEscrow>().AsNoTracking().FirstOrDefaultAsync(x => x.ContractsId == c.ContractsId, ct);
        var escrowTx = escrow is null ? [] : await _context.Set<EscrowTransaction>().AsNoTracking().Where(x => x.ContractEscrowId == escrow.ContractEscrowId).OrderByDescending(x => x.CreatedAt).Take(100).Select(x => new AdminContractReportLedger(x.EscrowTransactionId, x.MilestonesId, x.Amount, x.Type, x.Status, x.CreatedAt)).ToListAsync(ct);
        var walletTx = await _context.Set<WalletTransaction>().AsNoTracking().Where(x => x.ContractsId == c.ContractsId).OrderByDescending(x => x.CreatedAt).Take(100).Select(x => new AdminContractReportLedger(x.WalletTransactionsId, x.MilestonesId, x.TokenAmount, x.Type, x.Status, x.CreatedAt)).ToListAsync(ct);
        var refund = r.MilestoneId.HasValue ? escrowTx.Where(x => x.MilestoneId == r.MilestoneId && x.Type == (int)EscrowTransactionType.RefundToClient && x.Status == (int)EscrowTransactionStatus.Succeeded).Sum(x => x.Amount) : 0;
        var penalty = r.MilestoneId.HasValue ? escrowTx.Where(x => x.MilestoneId == r.MilestoneId && x.Type == (int)EscrowTransactionType.DisputePenalty && x.Status == (int)EscrowTransactionStatus.Succeeded).Sum(x => x.Amount) : 0;
        var conversationIds = await _context.Set<Conversation>().AsNoTracking().Where(x => x.ContractsId == c.ContractsId && (x.ConversationType == (int)ConversationType.ContractWorkroom || (related != null && x.DisputesId == related.DisputesId))).Select(x => x.ConversationsId).ToListAsync(ct);
        var since = r.CreatedAt.AddDays(-7);
        var messages = await _context.Set<Message>().AsNoTracking().Where(x => conversationIds.Contains(x.ConversationsId) && x.SentAt >= since && x.DeletedForEveryoneAt == null).OrderByDescending(x => x.SentAt).Take(100).OrderBy(x => x.SentAt).Select(x => new AdminContractReportMessage(x.MessagesId, x.ConversationsId, x.SenderUserId, x.SenderUser != null ? x.SenderUser.FullName : null, x.MessageType, x.Content, x.SentAt)).ToListAsync(ct);
        var audits = await LoadAudits(r.ReportContractId, c.ContractsId, related?.DisputesId, ct);
        var client = Party(c.ClientProfiles.User, "Client"); var freelancer = c.FreelancerProfiles is null ? null : Party(c.FreelancerProfiles.User, "Freelancer");
        var reporter = r.ReporterId == client.UserId ? client : freelancer ?? Party(r.Reporter, "Freelancer");
        var respondent = r.RespondentId == client.UserId ? client : r.RespondentId.HasValue ? freelancer : null;
        var attachmentUploaderIds = r.ReportContractAttachments.Where(x => x.UploadedByUserId.HasValue).Select(x => x.UploadedByUserId!.Value).Distinct().ToList();
        var uploaderNames = await _context.Set<User>().AsNoTracking().Where(x => attachmentUploaderIds.Contains(x.UserId)).ToDictionaryAsync(x => x.UserId, x => x.FullName, ct);
        var copiedNames = related is null ? new HashSet<string>() : (await _context.Set<DisputeEvidence>().AsNoTracking().Where(x => x.DisputesId == related.DisputesId && x.FileName != null).Select(x => x.FileName!).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var final = IsFinal(r.AdminReviewStatus); var linkedOpen = related is not null && related.Status is not (int)DisputeStatus.Resolved and not (int)DisputeStatus.Closed;
        var result = new AdminContractReportDetail(r.ReportContractId, r.IssueType, r.Description, r.DesiredResolution, r.Status, r.AdminReviewStatus, r.CreatedAt, r.UpdatedAt,
            r.ResolutionAction, r.AdminResolutionAction, r.AdminResolutionNote, r.AssignedAdminId, r.AssignedAdmin?.FullName, r.AssignedAt, related?.DisputesId, related?.Status,
            reporter, respondent, client, freelancer, c.ContractsId, c.Title, c.Status, c.TotalBudget, c.StartDate, c.EndDate, c.JobPostsId, c.JobPosts.Title, c.ProposalsId,
            c.Status == (int)ContractStatus.Disputed, await _context.Set<ReportContract>().CountAsync(x => x.ContractId == c.ContractsId, ct), await _context.Set<Dispute>().CountAsync(x => x.ContractsId == c.ContractsId, ct),
            r.Milestone is null ? null : new(r.Milestone.MilestonesId, r.Milestone.Title, r.Milestone.Amount, r.Milestone.Status, r.Milestone.SubmittedAt, r.Milestone.ApprovedAt, r.Milestone.ReleasedAmount, refund, penalty),
            r.ReportContractAttachments.OrderBy(x => x.UploadedAt).Select(x => new AdminContractReportAttachment(x.ReportContractAttachmentId, x.FileName, x.ContentType, x.FileSize, x.UploadedAt, x.UploadedByUserId, x.UploadedByUserId.HasValue ? uploaderNames.GetValueOrDefault(x.UploadedByUserId.Value) : r.Reporter.FullName, copiedNames.Contains(x.FileName))).ToList(),
            r.Explanation, r.ProposedResolution, r.RejectReason, r.RespondedAt, r.ResolvedAt, escrow?.RequiredAmount ?? c.TotalBudget, escrow?.FundedAmount ?? 0, escrow?.ReleasedAmount ?? 0, escrow is null ? 0 : Math.Max(0, escrow.FundedAmount - escrow.ReleasedAmount), escrowTx, walletTx, messages,
            r.AdminNotes.Where(x => x.IsActive).OrderBy(x => x.CreatedAt).Select(x => new AdminContractReportNote(x.ReportContractAdminNoteId, x.AdminUserId, x.AdminUser.FullName, x.Content, x.CreatedAt, x.UpdatedAt)).ToList(),
            r.InformationRequests.OrderBy(x => x.CreatedAt).Select(x => new AdminContractInformationRequest(x.InformationRequestId, x.RequestId, x.TargetUserId, x.TargetUser.FullName, x.Message, x.RequestedEvidenceOrClarification, x.DueAt, x.Status, x.CreatedAt, x.RespondedAt)).ToList(), audits,
            !final, !final, !final && !linkedOpen, !final, !final && related is null && r.Status != (int)ContractReportStatus.Resolved, !final && related is null && r.Status != (int)ContractReportStatus.Resolved);
        if (q.AuditSensitiveAccess && q.AdminId.HasValue)
        {
            _audit.Add(q.AdminId.Value, AdminAuditActions.ContractReportInvestigationViewed, nameof(ReportContract), r.ReportContractId, null,
                new { r.ContractId, r.ReporterId, r.RespondentId, messageCount = messages.Count, attachmentCount = r.ReportContractAttachments.Count });
            await _context.SaveChangesAsync(ct);
        }
        return result;
    }

    public Task<IReadOnlyList<AdminContractReportAudit>> Handle(GetAdminContractReportAuditQuery q, CancellationToken ct) => LoadAudits(q.ReportId, null, null, ct);
    private async Task<IReadOnlyList<AdminContractReportAudit>> LoadAudits(Guid reportId, Guid? contractId, Guid? disputeId, CancellationToken ct) => await _context.Set<AdminAuditLog>().AsNoTracking().Where(x =>
        (x.EntityType == nameof(ReportContract) && x.EntityId == reportId) || (contractId.HasValue && x.EntityType == nameof(Contract) && x.EntityId == contractId) || (disputeId.HasValue && x.EntityType == nameof(Dispute) && x.EntityId == disputeId))
        .OrderByDescending(x => x.CreatedAt).Select(x => new AdminContractReportAudit(x.AdminAuditLogsId, x.AdminId, x.Admin.FullName, x.Action, x.OldValues, x.NewValues, x.CorrelationId, x.CreatedAt)).ToListAsync(ct);
    private static AdminContractReportParty Party(User u, string role) => new(u.UserId, u.FullName, u.Email, role, u.AccountStatus, u.ViolationCount, u.IsFlagged);
    internal static bool IsFinal(int status) => status is (int)ContractReportAdminStatus.Closed or (int)ContractReportAdminStatus.Dismissed or (int)ContractReportAdminStatus.Escalated or (int)ContractReportAdminStatus.LinkedToDispute;
}

public sealed record AssignContractReportRequest(Guid? AdminId);
public sealed record AssignContractReportCommand(Guid ActorAdminId, Guid ReportId, Guid? AdminId) : IRequest<AdminContractReportDetail>;
public sealed record AddContractReportNoteCommand(Guid AdminId, Guid ReportId, string Content) : IRequest<AdminContractReportDetail>;
public sealed record RequestContractReportInformationRequest(Guid RequestId, ContractReportInformationTarget Target, string Message, string? RequestedEvidenceOrClarification, DateTime? DueAt);
public sealed record RequestContractReportInformationCommand(Guid AdminId, Guid ReportId, RequestContractReportInformationRequest Request) : IRequest<AdminContractReportDetail>;
public sealed record CloseContractReportRequest(ContractReportAdminResolutionAction ResolutionAction, string ResolutionSummary, string? InternalNote);
public sealed record CloseContractReportCommand(Guid AdminId, Guid ReportId, CloseContractReportRequest Request) : IRequest<AdminContractReportDetail>;
public sealed record DismissContractReportRequest(string Reason, string? InternalNote);
public sealed record DismissContractReportCommand(Guid AdminId, Guid ReportId, DismissContractReportRequest Request) : IRequest<AdminContractReportDetail>;
public sealed record LinkContractReportDisputeRequest(Guid DisputeId, string Reason);
public sealed record LinkContractReportDisputeCommand(Guid AdminId, Guid ReportId, LinkContractReportDisputeRequest Request) : IRequest<AdminContractReportDetail>;

public sealed class AdminContractReportMutationHandler :
    IRequestHandler<AssignContractReportCommand, AdminContractReportDetail>, IRequestHandler<AddContractReportNoteCommand, AdminContractReportDetail>,
    IRequestHandler<RequestContractReportInformationCommand, AdminContractReportDetail>, IRequestHandler<CloseContractReportCommand, AdminContractReportDetail>,
    IRequestHandler<DismissContractReportCommand, AdminContractReportDetail>, IRequestHandler<LinkContractReportDisputeCommand, AdminContractReportDetail>
{
    private readonly IApplicationDbContext _context; private readonly IAdminAuditService _audit; private readonly IMediator _mediator; private readonly IDateTimeService _clock; private readonly INotificationService _notifications;
    public AdminContractReportMutationHandler(IApplicationDbContext context, IAdminAuditService audit, IMediator mediator, IDateTimeService clock, INotificationService notifications) { _context = context; _audit = audit; _mediator = mediator; _clock = clock; _notifications = notifications; }
    public async Task<AdminContractReportDetail> Handle(AssignContractReportCommand q, CancellationToken ct)
    {
        await using var tx = await Begin(q.ActorAdminId, q.ReportId, ct); var r = await Load(q.ReportId, ct); EnsureOpen(r);
        var target = q.AdminId ?? q.ActorAdminId; var admin = await _context.Set<User>().FirstOrDefaultAsync(x => x.UserId == target && x.Role == (int)UserRole.Admin && x.IsActive && x.AccountStatus == (int)AccountStatus.Active, ct) ?? throw new BadRequestException("The assigned user must be an active administrator.");
        var old = new { r.AssignedAdminId, r.AssignedAt, r.AdminReviewStatus }; var action = r.AssignedAdminId.HasValue ? AdminAuditActions.ContractReportReassigned : AdminAuditActions.ContractReportAssigned;
        r.AssignedAdminId = admin.UserId; r.AssignedAt = _clock.UtcNow; r.AdminReviewStatus = (int)ContractReportAdminStatus.UnderReview; r.UpdatedAt = _clock.UtcNow;
        _audit.Add(q.ActorAdminId, action, nameof(ReportContract), r.ReportContractId, old, new { r.AssignedAdminId, r.AssignedAt, r.AdminReviewStatus, r.ContractId, r.ReporterId, r.RespondentId }); await SaveCommit(tx, ct); return await Detail(r.ReportContractId, ct);
    }
    public async Task<AdminContractReportDetail> Handle(AddContractReportNoteCommand q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q.Content)) throw new BadRequestException("Internal note content is required.");
        await using var tx = await Begin(q.AdminId, q.ReportId, ct); var r = await Load(q.ReportId, ct);
        var note = new ReportContractAdminNote { ReportContractAdminNoteId = Guid.NewGuid(), ReportContractId = r.ReportContractId, AdminUserId = q.AdminId, Content = q.Content.Trim(), CreatedAt = _clock.UtcNow };
        _context.Set<ReportContractAdminNote>().Add(note); r.UpdatedAt = _clock.UtcNow; _audit.Add(q.AdminId, AdminAuditActions.ContractReportInternalNoteAdded, nameof(ReportContract), r.ReportContractId, null, new { noteId = note.ReportContractAdminNoteId, r.ContractId }); await SaveCommit(tx, ct); return await Detail(r.ReportContractId, ct);
    }
    public async Task<AdminContractReportDetail> Handle(RequestContractReportInformationCommand q, CancellationToken ct)
    {
        if (q.Request.RequestId == Guid.Empty) throw new BadRequestException("Request ID is required."); if (string.IsNullOrWhiteSpace(q.Request.Message)) throw new BadRequestException("A request message is required."); if (q.Request.DueAt.HasValue && q.Request.DueAt <= _clock.UtcNow) throw new BadRequestException("Due date must be in the future.");
        await using var tx = await Begin(q.AdminId, q.ReportId, ct); var r = await Load(q.ReportId, ct); EnsureOpen(r);
        var targets = q.Request.Target switch { ContractReportInformationTarget.Reporter => new[] { r.ReporterId }, ContractReportInformationTarget.Respondent when r.RespondentId.HasValue => new[] { r.RespondentId.Value }, ContractReportInformationTarget.Both when r.RespondentId.HasValue => new[] { r.ReporterId, r.RespondentId.Value }, _ => throw new BadRequestException("The selected information-request target is unavailable.") };
        if (await _context.Set<ReportContractInformationRequest>().AnyAsync(x => x.ReportContractId == r.ReportContractId && x.RequestId == q.Request.RequestId, ct)) return await Detail(r.ReportContractId, ct);
        var rows = targets.Distinct().Select(target => new ReportContractInformationRequest { InformationRequestId = Guid.NewGuid(), RequestId = q.Request.RequestId, ReportContractId = r.ReportContractId, RequestedByAdminId = q.AdminId, TargetUserId = target, Message = q.Request.Message.Trim(), RequestedEvidenceOrClarification = q.Request.RequestedEvidenceOrClarification?.Trim(), DueAt = q.Request.DueAt, Status = (int)ContractReportInformationRequestStatus.Pending, CreatedAt = _clock.UtcNow }).ToList();
        _context.Set<ReportContractInformationRequest>().AddRange(rows); r.AssignedAdminId ??= q.AdminId; r.AssignedAt ??= _clock.UtcNow; r.AdminReviewStatus = (int)ContractReportAdminStatus.AwaitingInformation; r.UpdatedAt = _clock.UtcNow;
        _audit.Add(q.AdminId, AdminAuditActions.ContractReportInformationRequested, nameof(ReportContract), r.ReportContractId, null, new { q.Request.RequestId, targets, q.Request.DueAt, r.ContractId, r.ReporterId, r.RespondentId }); await SaveCommit(tx, ct);
        foreach (var target in targets) { try { await _notifications.CreateNotificationAsync(target, NotificationType.ReportUpdate, "Additional contract report information requested", q.Request.Message.Trim(), r.ReportContractId, nameof(ReportContract), ct); } catch { } }
        return await Detail(r.ReportContractId, ct);
    }
    public async Task<AdminContractReportDetail> Handle(CloseContractReportCommand q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q.Request.ResolutionSummary)) throw new BadRequestException("Resolution summary is required.");
        await using var tx = await Begin(q.AdminId, q.ReportId, ct); var r = await Load(q.ReportId, ct); EnsureOpen(r);
        var linkedOpen = await _context.Set<Dispute>().AnyAsync(x => x.RelatedReportId == r.ReportContractId && x.Status != (int)DisputeStatus.Resolved && x.Status != (int)DisputeStatus.Closed, ct); if (linkedOpen) throw new ConflictException("A Contract Report cannot be closed while its related Dispute remains open.");
        var old = Snapshot(r); r.AdminReviewStatus = (int)ContractReportAdminStatus.Closed; r.AdminResolutionAction = (int)q.Request.ResolutionAction; r.AdminResolutionNote = q.Request.ResolutionSummary.Trim(); r.Status = (int)ContractReportStatus.Resolved; r.ResolvedBy = q.AdminId; r.ResolvedAt = _clock.UtcNow; r.UpdatedAt = _clock.UtcNow; AddOptionalNote(r, q.AdminId, q.Request.InternalNote);
        _audit.Add(q.AdminId, AdminAuditActions.ContractReportClosed, nameof(ReportContract), r.ReportContractId, old, Snapshot(r)); await SaveCommit(tx, ct); return await Detail(r.ReportContractId, ct);
    }
    public async Task<AdminContractReportDetail> Handle(DismissContractReportCommand q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q.Request.Reason)) throw new BadRequestException("Dismissal reason is required.");
        await using var tx = await Begin(q.AdminId, q.ReportId, ct); var r = await Load(q.ReportId, ct); EnsureOpen(r); var old = Snapshot(r);
        r.AdminReviewStatus = (int)ContractReportAdminStatus.Dismissed; r.AdminResolutionNote = q.Request.Reason.Trim(); r.Status = (int)ContractReportStatus.Resolved; r.ResolvedBy = q.AdminId; r.ResolvedAt = _clock.UtcNow; r.UpdatedAt = _clock.UtcNow; AddOptionalNote(r, q.AdminId, q.Request.InternalNote);
        _audit.Add(q.AdminId, AdminAuditActions.ContractReportDismissed, nameof(ReportContract), r.ReportContractId, old, Snapshot(r)); await SaveCommit(tx, ct); return await Detail(r.ReportContractId, ct);
    }
    public async Task<AdminContractReportDetail> Handle(LinkContractReportDisputeCommand q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q.Request.Reason)) throw new BadRequestException("A linking reason is required.");
        await using var tx = await Begin(q.AdminId, q.ReportId, ct); var r = await Load(q.ReportId, ct); EnsureOpen(r); await tx.AcquireTransactionLockAsync(ContractEscrowLock.ForContract(r.ContractId), ct);
        var dispute = await _context.Set<Dispute>().Include(x => x.Contracts).ThenInclude(x => x.ClientProfiles).Include(x => x.Contracts).ThenInclude(x => x.FreelancerProfiles).FirstOrDefaultAsync(x => x.DisputesId == q.Request.DisputeId, ct) ?? throw new NotFoundException("Dispute does not exist.");
        if (dispute.ContractsId != r.ContractId) throw new BadRequestException("The Dispute and Contract Report must belong to the same Contract.");
        if (dispute.Status is (int)DisputeStatus.Resolved or (int)DisputeStatus.Closed) throw new ConflictException("A finalized Dispute cannot be linked.");
        if (dispute.RelatedReportId.HasValue && dispute.RelatedReportId != r.ReportContractId) throw new ConflictException("The Dispute is already linked to another Contract Report.");
        if (await _context.Set<Dispute>().AnyAsync(x => x.RelatedReportId == r.ReportContractId && x.DisputesId != dispute.DisputesId, ct)) throw new ConflictException("The Contract Report is already linked to another Dispute.");
        var participantIds = new[] { dispute.Contracts.ClientProfiles.UserId, dispute.Contracts.FreelancerProfiles?.UserId }.Where(x => x.HasValue).Select(x => x!.Value).ToHashSet(); if (!participantIds.Contains(r.ReporterId) || (r.RespondentId.HasValue && !participantIds.Contains(r.RespondentId.Value))) throw new BadRequestException("The Report and Dispute participants do not match.");
        var old = Snapshot(r); dispute.RelatedReportId = r.ReportContractId; r.Status = (int)ContractReportStatus.Escalated; r.IsEscalatedToDispute = true; r.AdminReviewStatus = (int)ContractReportAdminStatus.LinkedToDispute; r.AdminResolutionNote = q.Request.Reason.Trim(); r.UpdatedAt = _clock.UtcNow;
        _audit.Add(q.AdminId, AdminAuditActions.ContractReportLinkedToDispute, nameof(ReportContract), r.ReportContractId, old, new { report = Snapshot(r), disputeId = dispute.DisputesId, r.ContractId, r.ReporterId, r.RespondentId }); await SaveCommit(tx, ct); return await Detail(r.ReportContractId, ct);
    }
    private async Task<IApplicationDbContextTransaction> Begin(Guid adminId, Guid reportId, CancellationToken ct) { await EnsureAdmin(adminId, ct); var tx = await _context.BeginTransactionAsync(ct); await tx.AcquireTransactionLockAsync(ReportContractLock.ForReport(reportId), ct); return tx; }
    private async Task EnsureAdmin(Guid id, CancellationToken ct) { if (!await _context.Set<User>().AsNoTracking().AnyAsync(x => x.UserId == id && x.Role == (int)UserRole.Admin && x.IsActive && x.AccountStatus == (int)AccountStatus.Active, ct)) throw new ForbiddenAccessException("An active administrator account is required."); }
    private Task<ReportContract> Load(Guid id, CancellationToken ct) => _context.Set<ReportContract>().FirstOrDefaultAsync(x => x.ReportContractId == id, ct).ContinueWith(t => t.Result ?? throw new NotFoundException("Contract report does not exist."), ct);
    private static void EnsureOpen(ReportContract r) { if (AdminContractReportQueryHandler.IsFinal(r.AdminReviewStatus)) throw new ConflictException("This Contract Report is already finalized."); }
    private void AddOptionalNote(ReportContract r, Guid adminId, string? content) { if (!string.IsNullOrWhiteSpace(content)) _context.Set<ReportContractAdminNote>().Add(new() { ReportContractAdminNoteId = Guid.NewGuid(), ReportContractId = r.ReportContractId, AdminUserId = adminId, Content = content.Trim(), CreatedAt = _clock.UtcNow }); }
    private static object Snapshot(ReportContract r) => new { r.ContractId, r.ReporterId, r.RespondentId, r.Status, r.AdminReviewStatus, r.AdminResolutionAction, r.AdminResolutionNote, r.AssignedAdminId, r.ResolvedAt };
    private async Task SaveCommit(IApplicationDbContextTransaction tx, CancellationToken ct) { try { await _context.SaveChangesAsync(ct); await tx.CommitAsync(ct); } catch (DbUpdateConcurrencyException) { throw new ConflictException("The Contract Report changed while it was being processed. Refresh and retry."); } catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("InformationRequests", StringComparison.OrdinalIgnoreCase) == true || ex.InnerException?.Message.Contains("RelatedReportId", StringComparison.OrdinalIgnoreCase) == true) { throw new ConflictException("This action has already been processed or conflicts with another request."); } }
    private Task<AdminContractReportDetail> Detail(Guid id, CancellationToken ct) => _mediator.Send(new GetAdminContractReportDetailQuery(id), ct);
}

public sealed record GetContractReportAttachmentDownloadQuery(Guid AdminId, Guid ReportId, Guid AttachmentId) : IRequest<ContractReportAttachmentDownload>;
public sealed record ContractReportAttachmentDownload(Guid AttachmentId, string FileName, string DownloadUrl);
public sealed class GetContractReportAttachmentDownloadHandler : IRequestHandler<GetContractReportAttachmentDownloadQuery, ContractReportAttachmentDownload>
{
    private readonly IApplicationDbContext _context; private readonly IAdminAuditService _audit;
    public GetContractReportAttachmentDownloadHandler(IApplicationDbContext context, IAdminAuditService audit) { _context = context; _audit = audit; }
    public async Task<ContractReportAttachmentDownload> Handle(GetContractReportAttachmentDownloadQuery q, CancellationToken ct) { if (!await _context.Set<User>().AnyAsync(x => x.UserId == q.AdminId && x.Role == (int)UserRole.Admin && x.IsActive, ct)) throw new ForbiddenAccessException("Admin access is required."); var a = await _context.Set<ReportContractAttachment>().AsNoTracking().FirstOrDefaultAsync(x => x.ReportContractId == q.ReportId && x.ReportContractAttachmentId == q.AttachmentId, ct) ?? throw new NotFoundException("Contract Report attachment does not exist."); _audit.Add(q.AdminId, AdminAuditActions.ContractReportEvidenceDownloaded, nameof(ReportContract), q.ReportId, null, new { attachmentId = a.ReportContractAttachmentId, a.FileName }); await _context.SaveChangesAsync(ct); return new(a.ReportContractAttachmentId, a.FileName, a.FileUrl); }
}
