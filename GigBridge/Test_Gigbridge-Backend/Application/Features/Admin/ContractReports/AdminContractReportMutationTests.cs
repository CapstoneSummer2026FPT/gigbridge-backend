using Application.Common.Exceptions;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Admin.AuditLogs.Interfaces;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Features.Admin.ContractReports;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Contracts;
using Domain.Enums.Disputes;
using Domain.Enums.Reports;
using MediatR;
using NSubstitute;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Admin.ContractReports;

public sealed class AdminContractReportMutationTests
{
    [Fact]
    public async Task Assignment_UsesAuthenticatedAdmin_AndCreatesAudit()
    {
        var (handler, context, audit, _, report, admin, _) = Create();
        await handler.Handle(new AssignContractReportCommand(admin.UserId, report.ReportContractId, null), default);
        Assert.Equal(admin.UserId, report.AssignedAdminId);
        Assert.Equal((int)ContractReportAdminStatus.UnderReview, report.AdminReviewStatus);
        Assert.Equal(1, context.TransactionCommitCount);
        audit.Received(1).Add(admin.UserId, global::Application.Common.InternalServices.Admin.AuditLogs.Services.AdminAuditActions.ContractReportAssigned, nameof(ReportContract), report.ReportContractId, Arg.Any<object>(), Arg.Any<object>());
    }

    [Fact]
    public async Task InformationRequest_ForBothParticipants_IsPersistedOncePerTarget_AndRetryIsIdempotent()
    {
        var (handler, context, audit, _, report, admin, respondent) = Create();
        var rows = context.AddSet<ReportContractInformationRequest>(); var requestId = Guid.NewGuid();
        var command = new RequestContractReportInformationCommand(admin.UserId, report.ReportContractId,
            new(requestId, ContractReportInformationTarget.Both, "Provide delivery evidence.", "Upload the signed acceptance.", DateTime.UtcNow.AddDays(2)));
        await handler.Handle(command, default); await handler.Handle(command, default);
        Assert.Equal(2, rows.Entities.Count);
        Assert.Contains(rows.Entities, x => x.TargetUserId == report.ReporterId);
        Assert.Contains(rows.Entities, x => x.TargetUserId == respondent.UserId);
        Assert.Single(audit.ReceivedCalls().Where(call =>
            call.GetMethodInfo().Name == nameof(IAdminAuditService.Add) &&
            Equals(call.GetArguments()[1], global::Application.Common.InternalServices.Admin.AuditLogs.Services.AdminAuditActions.ContractReportInformationRequested)));
    }

    [Fact]
    public async Task Dismiss_FinalizesWithoutCreatingFinancialRows()
    {
        var (handler, context, _, _, report, admin, _) = Create();
        var wallet = context.AddSet<WalletTransaction>(); var escrow = context.AddSet<EscrowTransaction>();
        await handler.Handle(new DismissContractReportCommand(admin.UserId, report.ReportContractId, new("Insufficient basis.", "Reviewed evidence.")), default);
        Assert.Equal((int)ContractReportAdminStatus.Dismissed, report.AdminReviewStatus);
        Assert.Equal((int)ContractReportStatus.Resolved, report.Status);
        Assert.Empty(wallet.Entities); Assert.Empty(escrow.Entities);
        Assert.Single(context.Set<ReportContractAdminNote>().Cast<ReportContractAdminNote>());
    }

    [Fact]
    public async Task LinkDispute_RejectsDifferentContract()
    {
        var (handler, context, _, _, report, admin, _) = Create();
        var dispute = new Dispute { DisputesId = Guid.NewGuid(), ContractsId = Guid.NewGuid(), Status = (int)DisputeStatus.WaitingAdmin };
        context.AddSet(dispute);
        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(new LinkContractReportDisputeCommand(admin.UserId, report.ReportContractId, new(dispute.DisputesId, "Same issue.")), default));
        Assert.Null(dispute.RelatedReportId);
    }

    private static (AdminContractReportMutationHandler Handler, InMemoryApplicationDbContext Context, IAdminAuditService Audit, IMediator Mediator, ReportContract Report, User Admin, User Respondent) Create()
    {
        var context = new InMemoryApplicationDbContext(); var admin = User(UserRole.Admin); var reporter = User(UserRole.Client); var respondent = User(UserRole.Freelancer); var contractId = Guid.NewGuid();
        var report = new ReportContract { ReportContractId = Guid.NewGuid(), ContractId = contractId, ReporterId = reporter.UserId, RespondentId = respondent.UserId, Description = "Delayed work", DesiredResolution = "Deliver", Status = (int)ContractReportStatus.Pending, AdminReviewStatus = (int)ContractReportAdminStatus.Open, CreatedAt = DateTime.UtcNow };
        context.AddSet(admin, reporter, respondent); context.AddSet(report); context.AddSet<ReportContractAdminNote>(); context.AddSet<Dispute>();
        var contract = new Contract { ContractsId = contractId, Title = "Contract", Status = (int)ContractStatus.Active, ClientProfiles = new ClientProfile { UserId = reporter.UserId }, FreelancerProfiles = new FreelancerProfile { UserId = respondent.UserId } }; context.AddSet(contract);
        var audit = Substitute.For<IAdminAuditService>(); var mediator = Substitute.For<IMediator>(); var notifications = Substitute.For<INotificationService>();
        var clock = Substitute.For<IDateTimeService>(); clock.UtcNow.Returns(DateTime.UtcNow);
        return (new(context, audit, mediator, clock, notifications), context, audit, mediator, report, admin, respondent);
    }
    private static User User(UserRole role) => new() { UserId = Guid.NewGuid(), FullName = role.ToString(), Email = $"{role}@test.local", Role = (int)role, IsActive = true, AccountStatus = (int)AccountStatus.Active };
}
