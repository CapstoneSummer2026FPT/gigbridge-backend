using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Admin.AdminCredit.Commands;
using Application.Features.Admin.AdminCredit.DTOs;
using Application.Features.Wallets.Common.DTOs;
using Application.Features.Wallets.Common.TopUps.Confirm.Commands;
using Application.Features.Wallets.Common.TopUps.Create.Commands;
using Domain.Entities;
using Domain.Enums;
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
                    Role = (int)UserRole.Client
                });

            Wallets = Context.AddSet<UserWallet>();
            Transactions = Context.AddSet<WalletTransaction>();
        }

        public InMemoryApplicationDbContext Context { get; } = new();
        public DateTime Now { get; } = new(2026, 6, 11, 10, 0, 0, DateTimeKind.Utc);
        public Guid AdminUserId { get; } = Guid.NewGuid();
        public Guid ClientUserId { get; } = Guid.NewGuid();
        public TestDbSet<UserWallet> Wallets { get; }
        public TestDbSet<WalletTransaction> Transactions { get; }
    }

    private sealed class FakeWalletTopUpPaymentService : IWalletTopUpPaymentService
    {
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
