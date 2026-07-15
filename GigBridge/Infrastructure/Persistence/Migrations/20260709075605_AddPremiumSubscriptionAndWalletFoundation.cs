using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPremiumSubscriptionAndWalletFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "WalletTransactions",
                type: "integer",
                nullable: false,
                comment: "Enum WalletTransactionType: 0=AdminCredit, 1=TopUp, 2=EscrowHold, 3=EscrowRelease, 4=EscrowRefund, 5=Adjustment, 6=WithdrawalLock, 7=WithdrawalSuccess, 8=WithdrawalRefund, 9=WithdrawalFee, 10=SubscriptionPurchase, 11=PromotionPurchase",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum WalletTransactionType: 0=AdminCredit, 1=TopUp, 2=EscrowHold, 3=EscrowRelease, 4=EscrowRefund, 5=Adjustment, 6=WithdrawalLock, 7=WithdrawalSuccess, 8=WithdrawalRefund, 9=WithdrawalFee");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "UserWallets",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "UserWallets");

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "WalletTransactions",
                type: "integer",
                nullable: false,
                comment: "Enum WalletTransactionType: 0=AdminCredit, 1=TopUp, 2=EscrowHold, 3=EscrowRelease, 4=EscrowRefund, 5=Adjustment, 6=WithdrawalLock, 7=WithdrawalSuccess, 8=WithdrawalRefund, 9=WithdrawalFee",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum WalletTransactionType: 0=AdminCredit, 1=TopUp, 2=EscrowHold, 3=EscrowRelease, 4=EscrowRefund, 5=Adjustment, 6=WithdrawalLock, 7=WithdrawalSuccess, 8=WithdrawalRefund, 9=WithdrawalFee, 10=SubscriptionPurchase, 11=PromotionPurchase");
        }
    }
}
