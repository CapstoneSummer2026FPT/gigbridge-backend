using Application.Common.Exceptions;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Features.Contracts.Cancellation.Common.Cancel.Commands;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Auditing;
using Domain.Enums.Chat;
using Domain.Enums.Contracts;
using Domain.Enums.Notifications;
using Domain.Enums.Wallets;
using Microsoft.Extensions.Logging.Abstractions;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Contracts.Cancellation;

public class CancelContractCommandHandlerTests
{
    private const string AcceptFeeIdempotencyKeyPrefix = "SERVICE-FEE-ACCEPT-";

    [Fact]
    public async Task Cancel_BeforeOneMinuteSinceCreation_ThrowsBadRequest()
    {
        var fixture = new CancelContractFixture();
        var handler = CreateHandler(fixture, out _, out _, out _);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new CancelContractCommand(fixture.ContractId, fixture.ClientUserId),
                CancellationToken.None));

        Assert.Contains("cannot be cancelled until", exception.Message);
        Assert.Equal((int)ContractStatus.PendingContractConfirmation, fixture.Contract.Status);
    }

    [Fact]
    public async Task Cancel_ExactlyAtOneMinute_IsAllowed()
    {
        var fixture = new CancelContractFixture();
        var handler = CreateHandler(
            fixture, out _, out _, out _, nowOverride: fixture.Now.AddMinutes(1));

        var result = await handler.Handle(
            new CancelContractCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task Cancel_ByClientAfterWaitPeriod_CancelsContractAndRefundsFreelancerFee()
    {
        var fixture = new CancelContractFixture();
        fixture.ChargeFreelancerAcceptFee();
        var handler = CreateHandler(
            fixture, out var auditLog, out var realtime, out var notifications,
            nowOverride: fixture.Now.AddMinutes(5));

        var result = await handler.Handle(
            new CancelContractCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.Cancelled, result.Status);
        Assert.Equal((int)ContractStatus.Cancelled, fixture.Contract.Status);
        Assert.NotNull(fixture.Contract.CancelledAt);
        Assert.Equal(fixture.ClientUserId, fixture.Contract.CancelledByUserId);

        var freelancerWallet = fixture.Wallets.Entities.Single(wallet => wallet.UserId == fixture.FreelancerUserId);
        Assert.Equal(10m, freelancerWallet.AvailableTokens);

        var refund = Assert.Single(fixture.WalletTransactions.Entities,
            transaction => transaction.Type == (int)WalletTransactionType.ServiceFeeRefund);
        Assert.Equal(10m, refund.TokenAmount);
        Assert.Equal(fixture.FreelancerUserId, refund.UserId);

        var auditEntry = Assert.Single(auditLog.Entries);
        Assert.Equal(UserRole.Client, auditEntry.Role);
        Assert.Equal(AuditUserActionType.ContractCancelled, auditEntry.ActionType);

        var usersEvent = Assert.Single(realtime.UsersEvents);
        Assert.Equal("ContractCancelled", usersEvent.EventName);
        Assert.Contains(fixture.ClientUserId, usersEvent.UserIds);
        Assert.Contains(fixture.FreelancerUserId, usersEvent.UserIds);

        Assert.Equal(2, notifications.Notifications.Count);
        Assert.All(notifications.Notifications, notification =>
            Assert.Equal(NotificationType.ContractCancelled, notification.Type));
    }

    [Fact]
    public async Task Cancel_ReleasesTheAcceptedNegotiationOfferSlotForTheJobPost()
    {
        // UX_NegotiationOffers_AcceptedPerJobPost allows at most one Accepted offer per job
        // post, forever. Cancelling the contract must flip the originating offer away from
        // Accepted, or no future offer on the same job post could ever be accepted again.
        var fixture = new CancelContractFixture();
        var offer = fixture.AddAcceptedOffer();
        var handler = CreateHandler(
            fixture, out _, out _, out _, nowOverride: fixture.Now.AddMinutes(5));

        await handler.Handle(
            new CancelContractCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal((int)NegotiationOfferStatus.Cancelled, offer.Status);
        Assert.Equal(fixture.Now.AddMinutes(5), offer.RespondedAt);
    }

    [Fact]
    public async Task Cancel_ByFreelancerAfterWaitPeriod_Succeeds()
    {
        var fixture = new CancelContractFixture();
        var handler = CreateHandler(
            fixture, out _, out _, out _, nowOverride: fixture.Now.AddMinutes(5));

        var result = await handler.Handle(
            new CancelContractCommand(fixture.ContractId, fixture.FreelancerUserId),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.Cancelled, result.Status);
        Assert.Equal(fixture.FreelancerUserId, fixture.Contract.CancelledByUserId);
    }

    [Theory]
    [InlineData(ContractStatus.PendingEscrow)]
    [InlineData(ContractStatus.Active)]
    [InlineData(ContractStatus.Completed)]
    [InlineData(ContractStatus.Disputed)]
    public async Task Cancel_ContractProgressedPastCancellableWindow_ThrowsBadRequest(ContractStatus status)
    {
        var fixture = new CancelContractFixture();
        fixture.Contract.Status = (int)status;
        var handler = CreateHandler(
            fixture, out _, out _, out _, nowOverride: fixture.Now.AddMinutes(5));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new CancelContractCommand(fixture.ContractId, fixture.ClientUserId),
                CancellationToken.None));

        Assert.Equal((int)status, fixture.Contract.Status);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelledContract_ThrowsConflict()
    {
        var fixture = new CancelContractFixture();
        fixture.Contract.Status = (int)ContractStatus.Cancelled;
        var handler = CreateHandler(
            fixture, out _, out _, out _, nowOverride: fixture.Now.AddMinutes(5));

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new CancelContractCommand(fixture.ContractId, fixture.ClientUserId),
                CancellationToken.None));
    }

    [Fact]
    public async Task Cancel_ByNonParticipant_ThrowsForbidden()
    {
        var fixture = new CancelContractFixture();
        var handler = CreateHandler(
            fixture, out _, out _, out _, nowOverride: fixture.Now.AddMinutes(5));
        var outsiderUserId = Guid.NewGuid();

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            handler.Handle(
                new CancelContractCommand(fixture.ContractId, outsiderUserId),
                CancellationToken.None));

        Assert.Equal((int)ContractStatus.PendingContractConfirmation, fixture.Contract.Status);
    }

    [Fact]
    public async Task Cancel_MissingContract_ThrowsNotFound()
    {
        var fixture = new CancelContractFixture();
        var handler = CreateHandler(
            fixture, out _, out _, out _, nowOverride: fixture.Now.AddMinutes(5));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new CancelContractCommand(Guid.NewGuid(), fixture.ClientUserId),
                CancellationToken.None));
    }

    private static CancelContractCommandHandler CreateHandler(
        CancelContractFixture fixture,
        out CapturingUserAuditLogService auditLog,
        out CapturingChatRealtimeNotifier realtime,
        out RecordingNotificationService notifications,
        DateTime? nowOverride = null)
    {
        auditLog = new CapturingUserAuditLogService();
        realtime = new CapturingChatRealtimeNotifier();
        notifications = new RecordingNotificationService();
        IDateTimeService dateTimeService = new FixedDateTimeService(nowOverride ?? fixture.Now);

        return new CancelContractCommandHandler(
            fixture.Context,
            dateTimeService,
            realtime,
            notifications,
            auditLog,
            NullLogger<CancelContractCommandHandler>.Instance);
    }

    private sealed class CancelContractFixture
    {
        public InMemoryApplicationDbContext Context { get; } = new();
        public DateTime Now { get; } = new(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc);
        public Guid ClientUserId { get; } = Guid.NewGuid();
        public Guid FreelancerUserId { get; } = Guid.NewGuid();
        public Guid ClientProfileId { get; } = Guid.NewGuid();
        public Guid FreelancerProfileId { get; } = Guid.NewGuid();
        public Guid JobPostId { get; } = Guid.NewGuid();
        public Guid ContractId { get; } = Guid.NewGuid();
        public Contract Contract { get; }
        public TestDbSet<UserWallet> Wallets { get; }
        public TestDbSet<WalletTransaction> WalletTransactions { get; }
        public TestDbSet<NegotiationOffer> Offers { get; }

        public CancelContractFixture()
        {
            Contract = new Contract
            {
                ContractsId = ContractId,
                JobPostsId = JobPostId,
                ClientProfilesId = ClientProfileId,
                FreelancerProfilesId = FreelancerProfileId,
                Title = "Fixed contract",
                TotalBudget = 1_000m,
                Status = (int)ContractStatus.PendingContractConfirmation,
                CreatedAt = Now
            };

            Context.AddSet(new ClientProfile { ClientProfilesId = ClientProfileId, UserId = ClientUserId });
            Context.AddSet(new FreelancerProfile { FreelancerProfilesId = FreelancerProfileId, UserId = FreelancerUserId });
            Context.AddSet(Contract);
            Context.AddSet<Conversation>();
            Context.AddSet<Message>();
            Context.AddSet<PlatformRevenueEvent>();
            Wallets = Context.AddSet<UserWallet>();
            WalletTransactions = Context.AddSet<WalletTransaction>();
            Offers = Context.AddSet<NegotiationOffer>();
        }

        public NegotiationOffer AddAcceptedOffer()
        {
            var offer = new NegotiationOffer
            {
                NegotiationOfferId = Guid.NewGuid(),
                ConversationsId = Guid.NewGuid(),
                JobPostsId = JobPostId,
                ContractsId = ContractId,
                ClientProfilesId = ClientProfileId,
                FreelancerProfilesId = FreelancerProfileId,
                FinalPrice = 1_000m,
                Status = (int)NegotiationOfferStatus.Accepted,
                CreatedAt = Now,
                RespondedAt = Now
            };
            Offers.Add(offer);
            return offer;
        }

        public void ChargeFreelancerAcceptFee()
        {
            var wallet = new UserWallet
            {
                UserWalletsId = Guid.NewGuid(),
                UserId = FreelancerUserId,
                AvailableTokens = 0m,
                WithdrawableTokens = 0m,
                HeldTokens = 0m,
                CreatedAt = Now
            };
            Wallets.Add(wallet);
            WalletTransactions.Add(new WalletTransaction
            {
                WalletTransactionsId = Guid.NewGuid(),
                UserWalletsId = wallet.UserWalletsId,
                UserId = FreelancerUserId,
                ContractsId = ContractId,
                TokenAmount = 10m,
                VndAmount = 10m,
                BalanceSource = (int)WalletBalanceSource.Deposited,
                DepositedAmount = 10m,
                EarnedAmount = 0m,
                Type = (int)WalletTransactionType.Adjustment,
                Status = (int)WalletTransactionStatus.Succeeded,
                IdempotencyKey = $"{AcceptFeeIdempotencyKeyPrefix}{ContractId:N}",
                CreatedAt = Now,
                CompletedAt = Now
            });
        }
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public List<NotificationCall> Notifications { get; } = [];

        public Task CreateNotificationAsync(
            Guid userId,
            NotificationType type,
            string title,
            string? content = null,
            Guid? referenceId = null,
            string? referenceType = null,
            CancellationToken cancellationToken = default,
            string? metadata = null)
        {
            Notifications.Add(new NotificationCall(userId, type, title, content, referenceId, referenceType));
            return Task.CompletedTask;
        }

        public Task CreateBroadcastNotificationAsync(
            NotificationTarget target,
            NotificationType type,
            string title,
            string? content = null,
            Guid? referenceId = null,
            string? referenceType = null,
            Guid? targetUserId = null,
            bool sendEmail = false,
            Guid? createdByAdminId = null,
            DateTime? expiresAt = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed record NotificationCall(
        Guid UserId,
        NotificationType Type,
        string Title,
        string? Content,
        Guid? ReferenceId,
        string? ReferenceType);

    private sealed class FixedDateTimeService : IDateTimeService
    {
        public FixedDateTimeService(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }
}
