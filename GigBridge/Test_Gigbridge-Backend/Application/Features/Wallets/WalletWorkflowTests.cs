using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Common.Options;
using Application.Features.Admin.AdminCredit.Commands;
using Application.Features.Admin.AdminCredit.DTOs;
using Application.Features.Wallets.Common.BankAccounts.Create;
using Application.Features.Wallets.Common.DTOs;
using Application.Features.Wallets.Common.TopUps.Confirm.Commands;
using Application.Features.Wallets.Common.TopUps.Create.Commands;
using Application.Features.Wallets.Common.TopUps.Sync.Commands;
using Application.Features.Wallets.Common.Withdrawals.Create;
using Application.Features.Wallets.Common.Withdrawals.Sync;
using Application.Features.Wallets.Common.Withdrawals.Webhook;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Options;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Wallets;

public class WalletWorkflowTests
{
    [Fact]
    public async Task AdminCredit_CreatesWalletAndIsIdempotent()
    {
        var fixture = new WalletFixture();
        var handler = new AdminCreditWalletCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now));

        var request = new AdminCreditWalletRequest(120.5m, "demo credit", "admin-credit-1");

        await handler.Handle(
            new AdminCreditWalletCommand(fixture.AdminUserId, fixture.ClientUserId, request),
            CancellationToken.None);

        var duplicate = await handler.Handle(
            new AdminCreditWalletCommand(fixture.AdminUserId, fixture.ClientUserId, request),
            CancellationToken.None);

        var wallet = Assert.Single(fixture.Wallets.Entities);
        Assert.Equal(120.5m, wallet.AvailableTokens);
        Assert.Equal(0m, wallet.HeldTokens);
        Assert.Single(fixture.Transactions.Entities);
        Assert.Equal((int)WalletTransactionStatus.Succeeded, duplicate.Status);
    }

    [Fact]
    public async Task AdminCredit_NonAdminIsRejected()
    {
        var fixture = new WalletFixture();
        var handler = new AdminCreditWalletCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now));

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            handler.Handle(
                new AdminCreditWalletCommand(
                    fixture.ClientUserId,
                    fixture.ClientUserId,
                    new AdminCreditWalletRequest(10m, null, null)),
                CancellationToken.None));
    }

    [Fact]
    public async Task PayOsTopUp_CreatesPendingTransactionAndCallbackCreditsOnce()
    {
        var fixture = new WalletFixture();
        var paymentService = new FakeWalletTopUpPaymentService();
        var createHandler = new CreateWalletTopUpCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            paymentService);

        var topUp = await createHandler.Handle(
            new CreateWalletTopUpCommand(
                fixture.ClientUserId,
                new CreateWalletTopUpRequest(50m, "https://return", "https://cancel", "topup-1")),
            CancellationToken.None);

        Assert.Equal(50_000m, topUp.AmountVnd);
        Assert.Equal((int)WalletTransactionStatus.Pending, topUp.Status);

        var confirmHandler = new ConfirmWalletTopUpCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(5)),
            paymentService);

        var callback = new PayOsTopUpCallbackRequest(
            long.Parse(topUp.GatewayOrderCode),
            true,
            "00",
            null,
            "payos-ref-1",
            topUp.AmountVnd,
            "valid-signature",
            null);

        await confirmHandler.Handle(new ConfirmWalletTopUpCommand(callback), CancellationToken.None);
        await confirmHandler.Handle(new ConfirmWalletTopUpCommand(callback), CancellationToken.None);

        var wallet = Assert.Single(fixture.Wallets.Entities);
        Assert.Equal(50m, wallet.AvailableTokens);
        Assert.Single(fixture.Transactions.Entities);
        Assert.Equal((int)WalletTransactionStatus.Succeeded, fixture.Transactions.Entities[0].Status);
    }

    [Fact]
    public async Task PayOsTopUp_InvalidAmountIsRejected()
    {
        var fixture = new WalletFixture();
        var paymentService = new FakeWalletTopUpPaymentService();
        var createHandler = new CreateWalletTopUpCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            paymentService);

        var topUp = await createHandler.Handle(
            new CreateWalletTopUpCommand(
                fixture.ClientUserId,
                new CreateWalletTopUpRequest(50m, "https://return", "https://cancel", "topup-amount")),
            CancellationToken.None);

        var confirmHandler = new ConfirmWalletTopUpCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(5)),
            paymentService);

        var callback = new PayOsTopUpCallbackRequest(
            long.Parse(topUp.GatewayOrderCode),
            true,
            "00",
            null,
            "payos-ref-1",
            topUp.AmountVnd + 1,
            "valid-signature",
            null);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            confirmHandler.Handle(new ConfirmWalletTopUpCommand(callback), CancellationToken.None));

        var wallet = Assert.Single(fixture.Wallets.Entities);
        Assert.Equal(0m, wallet.AvailableTokens);
    }

    [Fact]
    public async Task PayOsTopUp_InvalidSignatureIsRejected()
    {
        var fixture = new WalletFixture();
        var paymentService = new FakeWalletTopUpPaymentService();
        var createHandler = new CreateWalletTopUpCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            paymentService);

        var topUp = await createHandler.Handle(
            new CreateWalletTopUpCommand(
                fixture.ClientUserId,
                new CreateWalletTopUpRequest(25m, "https://return", "https://cancel", "topup-signature")),
            CancellationToken.None);

        var confirmHandler = new ConfirmWalletTopUpCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(5)),
            paymentService);

        var callback = new PayOsTopUpCallbackRequest(
            long.Parse(topUp.GatewayOrderCode),
            true,
            "00",
            null,
            "payos-ref-1",
            topUp.AmountVnd,
            "invalid-signature",
            null);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            confirmHandler.Handle(new ConfirmWalletTopUpCommand(callback), CancellationToken.None));

        var wallet = Assert.Single(fixture.Wallets.Entities);
        Assert.Equal(0m, wallet.AvailableTokens);
    }

    [Fact]
    public async Task PayOsTopUp_FailedCallbackMarksTransactionFailedWithoutCreditingWallet()
    {
        var fixture = new WalletFixture();
        var paymentService = new FakeWalletTopUpPaymentService();
        var createHandler = new CreateWalletTopUpCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            paymentService);

        var topUp = await createHandler.Handle(
            new CreateWalletTopUpCommand(
                fixture.ClientUserId,
                new CreateWalletTopUpRequest(10m, "https://return", "https://cancel", "topup-failed")),
            CancellationToken.None);

        var confirmHandler = new ConfirmWalletTopUpCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(5)),
            paymentService);

        var callback = new PayOsTopUpCallbackRequest(
            long.Parse(topUp.GatewayOrderCode),
            false,
            "01",
            "cancelled",
            null,
            topUp.AmountVnd,
            "valid-signature",
            null);

        var result = await confirmHandler.Handle(new ConfirmWalletTopUpCommand(callback), CancellationToken.None);

        var wallet = Assert.Single(fixture.Wallets.Entities);
        Assert.Equal(0m, wallet.AvailableTokens);
        Assert.Equal((int)WalletTransactionStatus.Failed, result.Status);
        Assert.Equal("cancelled", fixture.Transactions.Entities[0].Note);
    }

    [Fact]
    public async Task PayOsTopUp_CallbackSuccessFalseWithCode00DoesNotCreditWallet()
    {
        var fixture = new WalletFixture();
        var paymentService = new FakeWalletTopUpPaymentService();
        var createHandler = new CreateWalletTopUpCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            paymentService);

        var topUp = await createHandler.Handle(
            new CreateWalletTopUpCommand(
                fixture.ClientUserId,
                new CreateWalletTopUpRequest(10m, "https://return", "https://cancel", "topup-code-00-false")),
            CancellationToken.None);

        var confirmHandler = new ConfirmWalletTopUpCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(5)),
            paymentService);

        var callback = new PayOsTopUpCallbackRequest(
            long.Parse(topUp.GatewayOrderCode),
            false,
            "00",
            "cancelled",
            null,
            topUp.AmountVnd,
            "valid-signature",
            null);

        var result = await confirmHandler.Handle(new ConfirmWalletTopUpCommand(callback), CancellationToken.None);

        var wallet = Assert.Single(fixture.Wallets.Entities);
        Assert.Equal(0m, wallet.AvailableTokens);
        Assert.Equal((int)WalletTransactionStatus.Failed, result.Status);
    }

    [Fact]
    public async Task PayOsTopUp_CreateWithDuplicateIdempotencyKeyReturnsExistingTransaction()
    {
        var fixture = new WalletFixture();
        var paymentService = new FakeWalletTopUpPaymentService();
        var createHandler = new CreateWalletTopUpCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            paymentService);

        var request = new CreateWalletTopUpRequest(15m, "https://return", "https://cancel", "topup-duplicate");

        var first = await createHandler.Handle(
            new CreateWalletTopUpCommand(fixture.ClientUserId, request),
            CancellationToken.None);
        var second = await createHandler.Handle(
            new CreateWalletTopUpCommand(fixture.ClientUserId, request),
            CancellationToken.None);

        Assert.Equal(first.WalletTransactionId, second.WalletTransactionId);
        Assert.Single(fixture.Transactions.Entities);
    }

    [Fact]
    public async Task PayOsTopUp_InvalidTokenAmountIsRejected()
    {
        var fixture = new WalletFixture();
        var createHandler = new CreateWalletTopUpCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new FakeWalletTopUpPaymentService());

        await Assert.ThrowsAsync<BadRequestException>(() =>
            createHandler.Handle(
                new CreateWalletTopUpCommand(
                    fixture.ClientUserId,
                    new CreateWalletTopUpRequest(0m, null, null, null)),
                CancellationToken.None));
    }

    [Fact]
    public async Task PayOsTopUp_SyncPaidStatusCreditsWalletOnce()
    {
        var fixture = new WalletFixture();
        var paymentService = new FakeWalletTopUpPaymentService();
        var createHandler = new CreateWalletTopUpCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            paymentService);

        var topUp = await createHandler.Handle(
            new CreateWalletTopUpCommand(
                fixture.ClientUserId,
                new CreateWalletTopUpRequest(30m, "https://return", "https://cancel", "topup-sync-paid")),
            CancellationToken.None);

        paymentService.StatusResult = new WalletTopUpStatusResult(
            long.Parse(topUp.GatewayOrderCode),
            "PAID",
            true,
            false,
            false,
            "payos-ref-sync",
            topUp.AmountVnd,
            null);

        var syncHandler = new SyncWalletTopUpCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(5)),
            paymentService);

        await syncHandler.Handle(
            new SyncWalletTopUpCommand(
                fixture.ClientUserId,
                new SyncPayOsTopUpRequest(long.Parse(topUp.GatewayOrderCode))),
            CancellationToken.None);

        var duplicate = await syncHandler.Handle(
            new SyncWalletTopUpCommand(
                fixture.ClientUserId,
                new SyncPayOsTopUpRequest(long.Parse(topUp.GatewayOrderCode))),
            CancellationToken.None);

        var wallet = Assert.Single(fixture.Wallets.Entities);
        Assert.Equal(30m, wallet.AvailableTokens);
        Assert.Equal((int)WalletTransactionStatus.Succeeded, duplicate.Status);
        Assert.Equal("payos-ref-sync", fixture.Transactions.Entities[0].GatewayTransactionCode);
    }

    [Fact]
    public async Task PayOsTopUp_SyncCancelledStatusDoesNotCreditWallet()
    {
        var fixture = new WalletFixture();
        var paymentService = new FakeWalletTopUpPaymentService();
        var createHandler = new CreateWalletTopUpCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            paymentService);

        var topUp = await createHandler.Handle(
            new CreateWalletTopUpCommand(
                fixture.ClientUserId,
                new CreateWalletTopUpRequest(20m, "https://return", "https://cancel", "topup-sync-cancelled")),
            CancellationToken.None);

        paymentService.StatusResult = new WalletTopUpStatusResult(
            long.Parse(topUp.GatewayOrderCode),
            "CANCELLED",
            false,
            true,
            false,
            null,
            topUp.AmountVnd,
            "CANCELLED");

        var syncHandler = new SyncWalletTopUpCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(5)),
            paymentService);

        var result = await syncHandler.Handle(
            new SyncWalletTopUpCommand(
                fixture.ClientUserId,
                new SyncPayOsTopUpRequest(long.Parse(topUp.GatewayOrderCode))),
            CancellationToken.None);

        var wallet = Assert.Single(fixture.Wallets.Entities);
        Assert.Equal(0m, wallet.AvailableTokens);
        Assert.Equal((int)WalletTransactionStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task BankAccount_CreateMasksAndProtectsAccountNumber()
    {
        var fixture = new WalletFixture();
        var protector = new FakeBankAccountProtector();
        var handler = new CreateBankAccountCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            protector);

        var result = await handler.Handle(
            new CreateBankAccountCommand(
                fixture.FreelancerUserId,
                new CreateBankAccountRequest("VCB", "Vietcombank", "1234 5678 9012", "NGUYEN VAN A", true)),
            CancellationToken.None);

        var account = Assert.Single(fixture.BankAccounts.Entities);
        Assert.Equal("********9012", result.AccountNumberMasked);
        Assert.Equal("protected:123456789012", account.AccountNumberEncrypted);
        Assert.NotEqual("123456789012", account.AccountNumberEncrypted);
        Assert.True(account.IsDefault);
    }

    [Fact]
    public async Task Withdrawal_CreateLocksAvailableBalanceAndCreatesOutbox()
    {
        var fixture = new WalletFixture();
        fixture.SeedFreelancerWallet(100m);
        fixture.SeedBankAccount();
        var handler = fixture.CreateWithdrawalHandler();

        var result = await handler.Handle(
            new CreateWithdrawalCommand(
                fixture.FreelancerUserId,
                new CreateWithdrawalRequest(30m, null, "withdrawal-lock")),
            CancellationToken.None);

        Assert.Equal((int)WithdrawalStatus.Pending, result.Status);
        var wallet = Assert.Single(fixture.Wallets.Entities.Where(wallet => wallet.UserId == fixture.FreelancerUserId));
        Assert.Equal(70m, wallet.AvailableTokens);
        Assert.Equal(30m, wallet.PendingWithdrawalTokens);
        Assert.Single(fixture.Withdrawals.Entities);
        Assert.Single(fixture.PayoutOutboxes.Entities);
        Assert.Contains(fixture.Transactions.Entities, transaction =>
            transaction.Type == (int)WalletTransactionType.WithdrawalLock &&
            transaction.Status == (int)WalletTransactionStatus.Succeeded);
    }

    [Fact]
    public async Task Withdrawal_CreateWithDuplicateIdempotencyKeyReturnsExistingWithdrawal()
    {
        var fixture = new WalletFixture();
        fixture.SeedFreelancerWallet(100m);
        fixture.SeedBankAccount();
        var handler = fixture.CreateWithdrawalHandler();
        var request = new CreateWithdrawalRequest(20m, null, "withdrawal-duplicate");

        var first = await handler.Handle(
            new CreateWithdrawalCommand(fixture.FreelancerUserId, request),
            CancellationToken.None);
        var second = await handler.Handle(
            new CreateWithdrawalCommand(fixture.FreelancerUserId, request),
            CancellationToken.None);

        Assert.Equal(first.WithdrawalId, second.WithdrawalId);
        Assert.Single(fixture.Withdrawals.Entities);
        Assert.Single(fixture.PayoutOutboxes.Entities);
    }

    [Fact]
    public async Task Withdrawal_CreateWithInsufficientBalanceIsRejected()
    {
        var fixture = new WalletFixture();
        fixture.SeedFreelancerWallet(5m);
        fixture.SeedBankAccount();
        var handler = fixture.CreateWithdrawalHandler();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new CreateWithdrawalCommand(
                    fixture.FreelancerUserId,
                    new CreateWithdrawalRequest(30m, null, "withdrawal-insufficient")),
                CancellationToken.None));

        var wallet = Assert.Single(fixture.Wallets.Entities.Where(wallet => wallet.UserId == fixture.FreelancerUserId));
        Assert.Equal(5m, wallet.AvailableTokens);
        Assert.Equal(0m, wallet.PendingWithdrawalTokens);
        Assert.Empty(fixture.Withdrawals.Entities);
    }

    [Fact]
    public async Task Withdrawal_SyncSuccessFinalizesOnce()
    {
        var fixture = new WalletFixture();
        var withdrawal = await fixture.CreatePendingWithdrawalAsync(40m, "withdrawal-success");
        var provider = new FakePayoutProvider
        {
            StatusResult = new PayoutProviderResult(
                PayoutProviderOutcome.Succeeded,
                "payout-1",
                "bank-ref-1",
                "SUCCESS",
                null,
                "{}")
        };
        var handler = fixture.CreateSyncHandler(provider);

        await handler.Handle(new SyncWithdrawalCommand(withdrawal.WithdrawalId, fixture.FreelancerUserId, false), CancellationToken.None);
        var duplicate = await handler.Handle(new SyncWithdrawalCommand(withdrawal.WithdrawalId, fixture.FreelancerUserId, false), CancellationToken.None);

        var wallet = Assert.Single(fixture.Wallets.Entities.Where(wallet => wallet.UserId == fixture.FreelancerUserId));
        Assert.Equal(60m, wallet.AvailableTokens);
        Assert.Equal(0m, wallet.PendingWithdrawalTokens);
        Assert.Equal((int)WithdrawalStatus.Success, duplicate.Status);
        Assert.Single(fixture.Transactions.Entities.Where(transaction =>
            transaction.Type == (int)WalletTransactionType.WithdrawalSuccess));
    }

    [Fact]
    public async Task Withdrawal_SyncFailureRefundsOnce()
    {
        var fixture = new WalletFixture();
        var withdrawal = await fixture.CreatePendingWithdrawalAsync(40m, "withdrawal-failed");
        var provider = new FakePayoutProvider
        {
            StatusResult = new PayoutProviderResult(
                PayoutProviderOutcome.Failed,
                "payout-1",
                null,
                "FAILED",
                "Invalid bank account",
                "{}")
        };
        var handler = fixture.CreateSyncHandler(provider);

        var result = await handler.Handle(
            new SyncWithdrawalCommand(withdrawal.WithdrawalId, fixture.FreelancerUserId, false),
            CancellationToken.None);

        var wallet = Assert.Single(fixture.Wallets.Entities.Where(wallet => wallet.UserId == fixture.FreelancerUserId));
        Assert.Equal(100m, wallet.AvailableTokens);
        Assert.Equal(0m, wallet.PendingWithdrawalTokens);
        Assert.Equal((int)WithdrawalStatus.Failed, result.Status);
        Assert.Single(fixture.Transactions.Entities.Where(transaction =>
            transaction.Type == (int)WalletTransactionType.WithdrawalRefund));
    }

    [Fact]
    public async Task Withdrawal_SyncRequiredKeepsBalanceLocked()
    {
        var fixture = new WalletFixture();
        var withdrawal = await fixture.CreatePendingWithdrawalAsync(25m, "withdrawal-sync-required");
        var provider = new FakePayoutProvider
        {
            StatusResult = new PayoutProviderResult(
                PayoutProviderOutcome.SyncRequired,
                null,
                null,
                "UNKNOWN_PROVIDER_STATUS",
                "Status is ambiguous",
                "{}")
        };
        var handler = fixture.CreateSyncHandler(provider);

        var result = await handler.Handle(
            new SyncWithdrawalCommand(withdrawal.WithdrawalId, fixture.FreelancerUserId, false),
            CancellationToken.None);

        var wallet = Assert.Single(fixture.Wallets.Entities.Where(wallet => wallet.UserId == fixture.FreelancerUserId));
        Assert.Equal(75m, wallet.AvailableTokens);
        Assert.Equal(25m, wallet.PendingWithdrawalTokens);
        Assert.Equal((int)WithdrawalStatus.SyncRequired, result.Status);
        Assert.Contains("Status is ambiguous", result.LastSyncError);
    }

    [Fact]
    public async Task Withdrawal_WebhookInvalidSignatureIsRejectedAndLogged()
    {
        var fixture = new WalletFixture();
        var provider = new FakePayoutProvider
        {
            WebhookResult = new PayoutWebhookVerificationResult(
                false,
                "event-1",
                null,
                null,
                PayoutProviderOutcome.SyncRequired,
                null,
                null,
                "invalid",
                "{\"eventId\":\"event-1\"}")
        };
        var handler = new HandlePayoutWebhookCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            provider);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new HandlePayoutWebhookCommand("{\"eventId\":\"event-1\"}", "bad-signature"),
                CancellationToken.None));

        var log = Assert.Single(fixture.WebhookLogs.Entities);
        Assert.Equal((int)PayoutWebhookProcessingStatus.Rejected, log.ProcessingStatus);
    }

    [Fact]
    public async Task Withdrawal_WebhookSuccessFinalizesOnceWhenDuplicated()
    {
        var fixture = new WalletFixture();
        var withdrawal = await fixture.CreatePendingWithdrawalAsync(35m, "withdrawal-webhook-success");
        var rawPayload = $"{{\"eventId\":\"event-success-1\",\"orderCode\":\"{withdrawal.ProviderOrderCode}\",\"status\":\"SUCCESS\"}}";
        var provider = new FakePayoutProvider
        {
            WebhookResult = new PayoutWebhookVerificationResult(
                true,
                "event-success-1",
                withdrawal.ProviderOrderCode,
                "payout-webhook-1",
                PayoutProviderOutcome.Succeeded,
                "bank-webhook-ref-1",
                "SUCCESS",
                null,
                rawPayload)
        };
        var handler = new HandlePayoutWebhookCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(5)),
            provider);

        await handler.Handle(new HandlePayoutWebhookCommand(rawPayload, "valid-signature"), CancellationToken.None);
        await handler.Handle(new HandlePayoutWebhookCommand(rawPayload, "valid-signature"), CancellationToken.None);

        var wallet = Assert.Single(fixture.Wallets.Entities.Where(wallet => wallet.UserId == fixture.FreelancerUserId));
        var storedWithdrawal = Assert.Single(fixture.Withdrawals.Entities);
        Assert.Equal(65m, wallet.AvailableTokens);
        Assert.Equal(0m, wallet.PendingWithdrawalTokens);
        Assert.Equal((int)WithdrawalStatus.Success, storedWithdrawal.Status);
        Assert.Single(fixture.WebhookLogs.Entities);
        Assert.Single(fixture.Transactions.Entities.Where(transaction =>
            transaction.Type == (int)WalletTransactionType.WithdrawalSuccess));
    }

    private sealed class WalletFixture
    {
        public WalletFixture()
        {
            Context.AddSet(
                new User
                {
                    UserId = AdminUserId,
                    FullName = "Admin",
                    Email = "admin@example.com",
                    Role = (int)UserRole.Admin
                },
                new User
                {
                    UserId = ClientUserId,
                    FullName = "Client",
                    Email = "client@example.com",
                    Role = (int)UserRole.Client,
                    IsActive = true
                },
                new User
                {
                    UserId = FreelancerUserId,
                    FullName = "Freelancer",
                    Email = "freelancer@example.com",
                    Role = (int)UserRole.Freelancer,
                    IsActive = true
                });

            Wallets = Context.AddSet<UserWallet>();
            Transactions = Context.AddSet<WalletTransaction>();
            BankAccounts = Context.AddSet<BankAccount>();
            Withdrawals = Context.AddSet<WalletWithdrawal>();
            PayoutOutboxes = Context.AddSet<PayoutOutbox>();
            WebhookLogs = Context.AddSet<PayoutWebhookLog>();
        }

        public InMemoryApplicationDbContext Context { get; } = new();
        public DateTime Now { get; } = new(2026, 6, 11, 10, 0, 0, DateTimeKind.Utc);
        public Guid AdminUserId { get; } = Guid.NewGuid();
        public Guid ClientUserId { get; } = Guid.NewGuid();
        public Guid FreelancerUserId { get; } = Guid.NewGuid();
        public TestDbSet<UserWallet> Wallets { get; }
        public TestDbSet<WalletTransaction> Transactions { get; }
        public TestDbSet<BankAccount> BankAccounts { get; }
        public TestDbSet<WalletWithdrawal> Withdrawals { get; }
        public TestDbSet<PayoutOutbox> PayoutOutboxes { get; }
        public TestDbSet<PayoutWebhookLog> WebhookLogs { get; }

        public void SeedFreelancerWallet(decimal availableTokens)
        {
            Wallets.Add(new UserWallet
            {
                UserWalletsId = Guid.NewGuid(),
                UserId = FreelancerUserId,
                AvailableTokens = availableTokens,
                HeldTokens = 0m,
                PendingWithdrawalTokens = 0m,
                CreatedAt = Now
            });
        }

        public BankAccount SeedBankAccount()
        {
            var bankAccount = new BankAccount
            {
                BankAccountId = Guid.NewGuid(),
                UserId = FreelancerUserId,
                BankCode = "VCB",
                BankName = "Vietcombank",
                AccountNumberEncrypted = "protected:123456789012",
                AccountNumberMasked = "********9012",
                AccountName = "NGUYEN VAN A",
                Status = (int)BankAccountStatus.Active,
                IsDefault = true,
                CreatedAt = Now
            };

            BankAccounts.Add(bankAccount);
            return bankAccount;
        }

        public CreateWithdrawalCommandHandler CreateWithdrawalHandler()
        {
            return new CreateWithdrawalCommandHandler(
                Context,
                new FixedDateTimeService(Now),
                Options.Create(new WalletWithdrawalOptions
                {
                    MinTokens = 1m,
                    MaxTokens = 1_000m,
                    DailyMaxTokens = 2_000m,
                    Provider = "PayOS"
                }));
        }

        public SyncWithdrawalCommandHandler CreateSyncHandler(FakePayoutProvider provider)
        {
            return new SyncWithdrawalCommandHandler(
                Context,
                new FixedDateTimeService(Now.AddMinutes(5)),
                provider);
        }

        public async Task<WithdrawalResponse> CreatePendingWithdrawalAsync(decimal tokenAmount, string idempotencyKey)
        {
            SeedFreelancerWallet(100m);
            SeedBankAccount();
            return await CreateWithdrawalHandler().Handle(
                new CreateWithdrawalCommand(
                    FreelancerUserId,
                    new CreateWithdrawalRequest(tokenAmount, null, idempotencyKey)),
                CancellationToken.None);
        }
    }

    private sealed class FakeWalletTopUpPaymentService : IWalletTopUpPaymentService
    {
        public WalletTopUpStatusResult? StatusResult { get; set; }

        public Task<WalletTopUpPaymentResult> CreatePaymentAsync(
            WalletTopUpPaymentRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new WalletTopUpPaymentResult(
                "PayOS",
                request.OrderCode.ToString(),
                $"plink-{request.OrderCode}",
                $"https://payos.test/{request.OrderCode}"));
        }

        public Task<WalletTopUpCallbackResult> VerifyCallbackAsync(
            WalletTopUpCallbackPayload payload,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new WalletTopUpCallbackResult(
                payload.Signature == "valid-signature",
                payload.OrderCode,
                payload.IsSucceeded,
                payload.GatewayTransactionCode,
                payload.AmountVnd,
                payload.FailureReason));
        }

        public Task<WalletTopUpStatusResult> GetPaymentStatusAsync(
            long orderCode,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                StatusResult ??
                new WalletTopUpStatusResult(
                    orderCode,
                    "PENDING",
                    false,
                    false,
                    false,
                    null,
                    null,
                    null));
        }
    }

    private sealed class FakeBankAccountProtector : IBankAccountProtector
    {
        public string Protect(string accountNumber)
        {
            return $"protected:{accountNumber}";
        }

        public string Unprotect(string protectedAccountNumber)
        {
            return protectedAccountNumber.Replace("protected:", string.Empty, StringComparison.Ordinal);
        }
    }

    private sealed class FakePayoutProvider : IPayoutProvider
    {
        public string ProviderName => "PayOS";

        public PayoutProviderResult StatusResult { get; set; } = new(
            PayoutProviderOutcome.Pending,
            null,
            null,
            "PENDING",
            null,
            "{}");

        public PayoutWebhookVerificationResult WebhookResult { get; set; } = new(
            true,
            "event-1",
            null,
            null,
            PayoutProviderOutcome.Pending,
            null,
            "PENDING",
            null,
            "{}");

        public Task<PayoutProviderResult> CreatePayoutAsync(
            PayoutCreateRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(StatusResult);
        }

        public Task<PayoutProviderResult> GetPayoutStatusAsync(
            PayoutStatusRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(StatusResult);
        }

        public Task<PayoutWebhookVerificationResult> VerifyWebhookAsync(
            PayoutWebhookVerificationRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(WebhookResult);
        }
    }

    private sealed class FixedDateTimeService : IDateTimeService
    {
        public FixedDateTimeService(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }
}
