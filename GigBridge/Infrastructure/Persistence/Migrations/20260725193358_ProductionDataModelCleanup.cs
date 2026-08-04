using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductionDataModelCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $cleanup$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "DisputeMessages" LIMIT 1) THEN
                        RAISE EXCEPTION
                            'ProductionDataModelCleanup aborted: DisputeMessages contains data. Archive it before retrying.';
                    END IF;

                    IF EXISTS (SELECT 1 FROM "PayoutWebhookLogs" LIMIT 1) THEN
                        RAISE EXCEPTION
                            'ProductionDataModelCleanup aborted: PayoutWebhookLogs contains data. Archive it before retrying.';
                    END IF;

                    IF EXISTS (SELECT 1 FROM "ProposalAttachments" LIMIT 1) THEN
                        RAISE EXCEPTION
                            'ProductionDataModelCleanup aborted: ProposalAttachments contains data. Archive it before retrying.';
                    END IF;

                    IF EXISTS (SELECT 1 FROM "RefreshTokens" LIMIT 1) THEN
                        RAISE EXCEPTION
                            'ProductionDataModelCleanup aborted: RefreshTokens contains data. Archive it before retrying.';
                    END IF;

                    IF EXISTS (SELECT 1 FROM "PaymentProofs" LIMIT 1) THEN
                        RAISE EXCEPTION
                            'ProductionDataModelCleanup aborted: PaymentProofs contains data. Archive it before retrying.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "ConversationParticipants"
                        WHERE "IsMuted" OR "IsPinned" OR "IsArchived"
                        LIMIT 1
                    ) THEN
                        RAISE EXCEPTION
                            'ProductionDataModelCleanup aborted: ConversationParticipants contains active mute, pin, or archive flags.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "TalentMatchRuns"
                        WHERE "CacheHit"
                        LIMIT 1
                    ) THEN
                        RAISE EXCEPTION
                            'ProductionDataModelCleanup aborted: TalentMatchRuns.CacheHit contains non-default data.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "Reports"
                        WHERE "AdminAttachmentUrl" IS NOT NULL
                           OR "AdminAttachmentFileName" IS NOT NULL
                        LIMIT 1
                    ) THEN
                        RAISE EXCEPTION
                            'ProductionDataModelCleanup aborted: Reports contains legacy admin attachment data.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "WorkExperiences"
                        WHERE "IsCurrentJob" IS TRUE
                        LIMIT 1
                    ) THEN
                        RAISE EXCEPTION
                            'ProductionDataModelCleanup aborted: WorkExperiences.IsCurrentJob contains non-default data.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "Disputes"
                        WHERE "AiSuggestedResolution" IS NOT NULL
                        LIMIT 1
                    ) THEN
                        RAISE EXCEPTION
                            'ProductionDataModelCleanup aborted: Disputes.AiSuggestedResolution contains data.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "Users"
                        WHERE "EmailVerificationToken" IS NOT NULL
                        LIMIT 1
                    ) THEN
                        RAISE EXCEPTION
                            'ProductionDataModelCleanup aborted: Users.EmailVerificationToken contains data.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "Users"
                        WHERE "TokenExpiry" IS NOT NULL
                        LIMIT 1
                    ) THEN
                        RAISE EXCEPTION
                            'ProductionDataModelCleanup aborted: Users.TokenExpiry contains data.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "Users"
                        GROUP BY lower(btrim("Email"))
                        HAVING count(*) > 1
                        LIMIT 1
                    ) THEN
                        RAISE EXCEPTION
                            'ProductionDataModelCleanup aborted: Users contains duplicate canonical email addresses.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "PortfolioItems"
                        WHERE "CategoryCategoriesId" IS NOT NULL
                        LIMIT 1
                    ) THEN
                        RAISE EXCEPTION
                            'ProductionDataModelCleanup aborted: PortfolioItems.CategoryCategoriesId contains data.';
                    END IF;

                    IF to_regclass('public."ClientProfiles_usr_UserId_key"') IS NULL THEN
                        RAISE EXCEPTION
                            'ProductionDataModelCleanup aborted: canonical ClientProfiles user index is missing.';
                    END IF;

                    IF to_regclass('public."FreelancerProfiles_usr_UserId_key"') IS NULL THEN
                        RAISE EXCEPTION
                            'ProductionDataModelCleanup aborted: canonical FreelancerProfiles user index is missing.';
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM pg_constraint
                        WHERE conname = 'AK_TalentMatchResults_TalentMatchRunId_FreelancerProfileId'
                    ) THEN
                        RAISE EXCEPTION
                            'ProductionDataModelCleanup aborted: TalentMatchResults alternate key is missing.';
                    END IF;
                END
                $cleanup$;
                """);

            migrationBuilder.Sql(
                """
                CREATE EXTENSION IF NOT EXISTS citext;

                UPDATE "Users"
                SET "Email" = lower(btrim("Email"))
                WHERE "Email" IS DISTINCT FROM lower(btrim("Email"));
                """);

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "citext",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "Notifications",
                type: "integer",
                nullable: false,
                comment: "Enum NotificationType: 0=NewJob, 1=ProposalReceived, 2=ProposalStatusChanged, 3=ContractStarted, 4=MilestoneUpdated, 5=PaymentProofUploaded, 6=PaymentConfirmed, 7=ChatMessage, 8=DisputeUpdate, 9=ReviewReceived, 10=SystemAlert, 11=AIInterviewInvite, 12=SubscriptionExpiring, 13=Schedule, 14=SubscriptionActivated, 15=SubscriptionCancelled, 16=PromotionActivated, 17=PromotionExpired, 18=RankProtectionActivated, 19=RankProtectionExpired, 20=ReportUpdate",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum NotificationType: 0=NewJob, 1=ProposalReceived, 2=ProposalStatusChanged, 3=ContractStarted, 4=MilestoneUpdated, 5=PaymentProofUploaded, 6=PaymentConfirmed, 7=ChatMessage, 8=DisputeUpdate, 9=ReviewReceived, 10=SystemAlert, 11=AIInterviewInvite, 12=SubscriptionExpiring, 13=Schedule, 14=SubscriptionActivated, 15=SubscriptionCancelled, 16=PromotionActivated, 17=PromotionExpired, 18=RankProtectionActivated, 19=RankProtectionExpired");

            migrationBuilder.DropForeignKey(
                name: "FK_PortfolioItems_Categories_CategoryCategoriesId",
                table: "PortfolioItems");

            migrationBuilder.DropTable(
                name: "DisputeMessages");

            migrationBuilder.DropTable(
                name: "PayoutWebhookLogs");

            migrationBuilder.DropTable(
                name: "ProposalAttachments");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "PaymentProofs");

            migrationBuilder.DropIndex(
                name: "UX_TalentMatchResults_Run_Freelancer",
                table: "TalentMatchResults");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioItems_CategoryCategoriesId",
                table: "PortfolioItems");

            migrationBuilder.DropIndex(
                name: "IX_FreelancerProfiles_UserId",
                table: "FreelancerProfiles");

            migrationBuilder.DropIndex(
                name: "IX_ClientProfiles_UserId",
                table: "ClientProfiles");

            migrationBuilder.DropColumn(
                name: "CategoryCategoriesId",
                table: "PortfolioItems");

            migrationBuilder.DropColumn(
                name: "EmailVerificationToken",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TokenExpiry",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsCurrentJob",
                table: "WorkExperiences");

            migrationBuilder.DropColumn(
                name: "CacheHit",
                table: "TalentMatchRuns");

            migrationBuilder.DropColumn(
                name: "AdminAttachmentFileName",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "AdminAttachmentUrl",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "AiSuggestedResolution",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "ConversationParticipants");

            migrationBuilder.DropColumn(
                name: "IsMuted",
                table: "ConversationParticipants");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "ConversationParticipants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "citext",
                oldMaxLength: 255);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "Notifications",
                type: "integer",
                nullable: false,
                comment: "Enum NotificationType: 0=NewJob, 1=ProposalReceived, 2=ProposalStatusChanged, 3=ContractStarted, 4=MilestoneUpdated, 5=PaymentProofUploaded, 6=PaymentConfirmed, 7=ChatMessage, 8=DisputeUpdate, 9=ReviewReceived, 10=SystemAlert, 11=AIInterviewInvite, 12=SubscriptionExpiring, 13=Schedule, 14=SubscriptionActivated, 15=SubscriptionCancelled, 16=PromotionActivated, 17=PromotionExpired, 18=RankProtectionActivated, 19=RankProtectionExpired",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum NotificationType: 0=NewJob, 1=ProposalReceived, 2=ProposalStatusChanged, 3=ContractStarted, 4=MilestoneUpdated, 5=PaymentProofUploaded, 6=PaymentConfirmed, 7=ChatMessage, 8=DisputeUpdate, 9=ReviewReceived, 10=SystemAlert, 11=AIInterviewInvite, 12=SubscriptionExpiring, 13=Schedule, 14=SubscriptionActivated, 15=SubscriptionCancelled, 16=PromotionActivated, 17=PromotionExpired, 18=RankProtectionActivated, 19=RankProtectionExpired, 20=ReportUpdate");

            migrationBuilder.AddColumn<string>(
                name: "EmailVerificationToken",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TokenExpiry",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryCategoriesId",
                table: "PortfolioItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCurrentJob",
                table: "WorkExperiences",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CacheHit",
                table: "TalentMatchRuns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AdminAttachmentFileName",
                table: "Reports",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdminAttachmentUrl",
                table: "Reports",
                type: "text",
                nullable: true,
                comment: "v1.2: Admin đính kèm bản hợp đồng lao động e-sign PDF cho tranh chấp thanh toán");

            migrationBuilder.AddColumn<string>(
                name: "AiSuggestedResolution",
                table: "Disputes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "ConversationParticipants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsMuted",
                table: "ConversationParticipants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "ConversationParticipants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Required Boolean columns need a temporary value for existing rows. Remove
            // that temporary database default so the rollback matches the original model.
            migrationBuilder.Sql(
                """
                ALTER TABLE "TalentMatchRuns" ALTER COLUMN "CacheHit" DROP DEFAULT;
                ALTER TABLE "ConversationParticipants" ALTER COLUMN "IsArchived" DROP DEFAULT;
                ALTER TABLE "ConversationParticipants" ALTER COLUMN "IsMuted" DROP DEFAULT;
                ALTER TABLE "ConversationParticipants" ALTER COLUMN "IsPinned" DROP DEFAULT;
                """);

            migrationBuilder.CreateTable(
                name: "DisputeMessages",
                columns: table => new
                {
                    DisputeMessagesId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    DisputesId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("DisputeMessages_pkey", x => x.DisputeMessagesId);
                    table.ForeignKey(
                        name: "DisputeMessages_disp_DisputesId_fkey",
                        column: x => x.DisputesId,
                        principalTable: "Disputes",
                        principalColumn: "DisputesId");
                    table.ForeignKey(
                        name: "DisputeMessages_usr_SenderId_fkey",
                        column: x => x.SenderId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "PayoutWebhookLogs",
                columns: table => new
                {
                    PayoutWebhookLogId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    WalletWithdrawalId = table.Column<Guid>(type: "uuid", nullable: true),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessingStatus = table.Column<int>(type: "integer", nullable: false, comment: "Enum PayoutWebhookProcessingStatus: 0=Pending, 1=Processed, 2=Rejected, 3=Failed"),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RawPayload = table.Column<string>(type: "jsonb", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    SignatureHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PayoutWebhookLogs_pkey", x => x.PayoutWebhookLogId);
                    table.ForeignKey(
                        name: "PayoutWebhookLogs_wwd_WalletWithdrawalId_fkey",
                        column: x => x.WalletWithdrawalId,
                        principalTable: "WalletWithdrawals",
                        principalColumn: "WalletWithdrawalId");
                });

            migrationBuilder.CreateTable(
                name: "ProposalAttachments",
                columns: table => new
                {
                    ProposalAttachmentsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ProposalsId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    FileUrl = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ProposalAttachments_pkey", x => x.ProposalAttachmentsId);
                    table.ForeignKey(
                        name: "ProposalAttachments_propo_ProposalsId_fkey",
                        column: x => x.ProposalsId,
                        principalTable: "Proposals",
                        principalColumn: "ProposalsId");
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Token = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("RefreshTokens_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "RefreshTokens_usr_UserId_fkey",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "PaymentProofs",
                columns: table => new
                {
                    PaymentProofsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    MilestonesId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DisputedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    FileUrl = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0, comment: "Enum PaymentProofStatus: 0=Pending, 1=Confirmed, 2=Disputed")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PaymentProofs_pkey", x => x.PaymentProofsId);
                    table.ForeignKey(
                        name: "PaymentProofs_mStone_MilestonesId_fkey",
                        column: x => x.MilestonesId,
                        principalTable: "Milestones",
                        principalColumn: "MilestonesId");
                    table.ForeignKey(
                        name: "PaymentProofs_usr_UploadedById_fkey",
                        column: x => x.UploadedById,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "UX_TalentMatchResults_Run_Freelancer",
                table: "TalentMatchResults",
                columns: new[] { "TalentMatchRunId", "FreelancerProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioItems_CategoryCategoriesId",
                table: "PortfolioItems",
                column: "CategoryCategoriesId");

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerProfiles_UserId",
                table: "FreelancerProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientProfiles_UserId",
                table: "ClientProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DisputeMessages_DisputesId_CreatedAt",
                table: "DisputeMessages",
                columns: new[] { "DisputesId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DisputeMessages_SenderId",
                table: "DisputeMessages",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutWebhookLogs_Provider_EventId",
                table: "PayoutWebhookLogs",
                columns: new[] { "Provider", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutWebhookLogs_Provider_SignatureHash",
                table: "PayoutWebhookLogs",
                columns: new[] { "Provider", "SignatureHash" });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutWebhookLogs_WalletWithdrawalId",
                table: "PayoutWebhookLogs",
                column: "WalletWithdrawalId");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalAttachments_ProposalsId",
                table: "ProposalAttachments",
                column: "ProposalsId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ExpiresAt",
                table: "RefreshTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProofs_MilestonesId",
                table: "PaymentProofs",
                column: "MilestonesId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProofs_Status",
                table: "PaymentProofs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProofs_UploadedById",
                table: "PaymentProofs",
                column: "UploadedById");

            migrationBuilder.AddForeignKey(
                name: "FK_PortfolioItems_Categories_CategoryCategoriesId",
                table: "PortfolioItems",
                column: "CategoryCategoriesId",
                principalTable: "Categories",
                principalColumn: "CategoriesId");
        }
    }
}
