using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P0ChatNegotiationFinalOffer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "Conversations_usr_User1Id_fkey",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "Conversations_usr_User2Id_fkey",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "Messages_usr_SenderId_fkey",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_IsRead",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_SenderId",
                table: "Messages");

            migrationBuilder.DropUniqueConstraint(
                name: "Conversations_usr_User1Id_usr_User2Id_cont_ContractsId_key",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_User1Id",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_User2Id",
                table: "Conversations");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Messages",
                newName: "EditedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Messages",
                newName: "SentAt");

            migrationBuilder.RenameIndex(
                name: "IX_Messages_ConversationsId_CreatedAt",
                table: "Messages",
                newName: "IX_Messages_ConversationsId_SentAt");

            migrationBuilder.AddColumn<string>(
                name: "ClientMessageId",
                table: "Messages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedForEveryoneAt",
                table: "Messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedForSenderAt",
                table: "Messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MessageType",
                table: "Messages",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "Enum MessageType: 0=Text, 1=Image, 2=File, 3=System, 4=FinalOffer, 5=ContractEvent, 6=MilestoneEvent, 7=PaymentEvent, 8=DisputeEvent");

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "Messages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplyToMessageId",
                table: "Messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SenderUserId",
                table: "Messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileExtension",
                table: "MessageAttachments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                table: "MessageAttachments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "MimeType",
                table: "MessageAttachments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StorageObjectKey",
                table: "MessageAttachments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageProvider",
                table: "MessageAttachments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ConversationType",
                table: "Conversations",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "Enum ConversationType: 0=JobNegotiation, 1=ContractWorkroom, 2=Dispute, 3=Support");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DisputesId",
                table: "Conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "JobPostsId",
                table: "Conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastMessageId",
                table: "Conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProposalsId",
                table: "Conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Conversations",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "Enum ConversationStatus: 0=Active, 1=Archived, 2=Closed");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Conversations",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Messages"
                SET
                    "SenderUserId" = "SenderId",
                    "MessageType" = COALESCE("Type", 0),
                    "DeletedForEveryoneAt" = CASE WHEN COALESCE("IsDeleted", false) THEN "SentAt" ELSE NULL END,
                    "EditedAt" = CASE WHEN COALESCE("IsEdited", false) THEN "EditedAt" ELSE NULL END;

                UPDATE "MessageAttachments"
                SET
                    "FileSizeBytes" = COALESCE("FileSize", 0),
                    "MimeType" = COALESCE("ContentType", 'application/octet-stream'),
                    "StorageProvider" = 'legacy';

                UPDATE "Conversations"
                SET
                    "CreatedByUserId" = "User1Id",
                    "ConversationType" = CASE WHEN COALESCE("Type", 0) = 1 THEN 1 ELSE 0 END,
                    "Status" = 0;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByUserId",
                table: "Conversations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Contracts",
                type: "integer",
                nullable: false,
                comment: "Enum ContractStatus: 0=Draft, 1=PendingFreelancerSelection, 2=InNegotiation, 3=PendingContractDetails, 4=PendingContractConfirmation, 5=PendingEscrow, 6=PendingSignature, 7=Active, 8=Completed, 9=Cancelled, 10=Disputed",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum ContractStatus: 0=Draft, 1=PendingFreelancerSelection, 2=PendingEscrow, 3=PendingSignature, 4=Active, 5=Completed, 6=Cancelled, 7=Disputed");

            migrationBuilder.Sql(
                """
                UPDATE "Contracts"
                SET "Status" = CASE "Status"
                    WHEN 2 THEN 5
                    WHEN 3 THEN 6
                    WHEN 4 THEN 7
                    WHEN 5 THEN 8
                    WHEN 6 THEN 9
                    WHEN 7 THEN 10
                    ELSE "Status"
                END;
                """);

            migrationBuilder.CreateTable(
                name: "ConversationParticipants",
                columns: table => new
                {
                    ConversationParticipantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ConversationsId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantRole = table.Column<int>(type: "integer", nullable: false, comment: "Enum ParticipantRole: 0=Client, 1=Freelancer, 2=Admin, 3=Support"),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    LeftAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastReadMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UnreadCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsMuted = table.Column<bool>(type: "boolean", nullable: false),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ConversationParticipants_pkey", x => x.ConversationParticipantId);
                    table.ForeignKey(
                        name: "ConversationParticipants_conv_ConversationsId_fkey",
                        column: x => x.ConversationsId,
                        principalTable: "Conversations",
                        principalColumn: "ConversationsId");
                    table.ForeignKey(
                        name: "ConversationParticipants_msg_LastReadMessageId_fkey",
                        column: x => x.LastReadMessageId,
                        principalTable: "Messages",
                        principalColumn: "MessagesId");
                    table.ForeignKey(
                        name: "ConversationParticipants_usr_UserId_fkey",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "ConversationParticipants"
                    ("ConversationParticipantId", "ConversationsId", "UserId", "ParticipantRole", "JoinedAt", "UnreadCount", "IsMuted", "IsPinned", "IsArchived")
                SELECT
                    gen_random_uuid(),
                    c."ConversationsId",
                    c."User1Id",
                    CASE u."Role"
                        WHEN 0 THEN 0
                        WHEN 1 THEN 1
                        WHEN 2 THEN 2
                        ELSE 3
                    END,
                    c."CreatedAt",
                    0,
                    false,
                    false,
                    false
                FROM "Conversations" c
                JOIN "Users" u ON u."UserId" = c."User1Id";

                INSERT INTO "ConversationParticipants"
                    ("ConversationParticipantId", "ConversationsId", "UserId", "ParticipantRole", "JoinedAt", "UnreadCount", "IsMuted", "IsPinned", "IsArchived")
                SELECT
                    gen_random_uuid(),
                    c."ConversationsId",
                    c."User2Id",
                    CASE u."Role"
                        WHEN 0 THEN 0
                        WHEN 1 THEN 1
                        WHEN 2 THEN 2
                        ELSE 3
                    END,
                    c."CreatedAt",
                    0,
                    false,
                    false,
                    false
                FROM "Conversations" c
                JOIN "Users" u ON u."UserId" = c."User2Id"
                WHERE c."User2Id" <> c."User1Id";
                """);

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "IsEdited",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "IsRead",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "SenderId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "MessageAttachments");

            migrationBuilder.DropColumn(
                name: "FileSize",
                table: "MessageAttachments");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "User1Id",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "User2Id",
                table: "Conversations");

            migrationBuilder.CreateTable(
                name: "NegotiationOffers",
                columns: table => new
                {
                    NegotiationOfferId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ConversationsId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPostsId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractsId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalsId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientProfilesId = table.Column<Guid>(type: "uuid", nullable: false),
                    FreelancerProfilesId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinalPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ScopeSummary = table.Column<string>(type: "text", nullable: true),
                    ClientNote = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Enum NegotiationOfferStatus: 0=PendingFreelancerConfirmation, 1=Accepted, 2=Rejected, 3=ChangeRequested, 4=Expired, 5=Cancelled"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("NegotiationOffers_pkey", x => x.NegotiationOfferId);
                    table.ForeignKey(
                        name: "NegotiationOffers_clPro_ClientProfilesId_fkey",
                        column: x => x.ClientProfilesId,
                        principalTable: "ClientProfiles",
                        principalColumn: "ClientProfilesId");
                    table.ForeignKey(
                        name: "NegotiationOffers_cont_ContractsId_fkey",
                        column: x => x.ContractsId,
                        principalTable: "Contracts",
                        principalColumn: "ContractsId");
                    table.ForeignKey(
                        name: "NegotiationOffers_conv_ConversationsId_fkey",
                        column: x => x.ConversationsId,
                        principalTable: "Conversations",
                        principalColumn: "ConversationsId");
                    table.ForeignKey(
                        name: "NegotiationOffers_flPro_FreelancerProfilesId_fkey",
                        column: x => x.FreelancerProfilesId,
                        principalTable: "FreelancerProfiles",
                        principalColumn: "FreelancerProfilesId");
                    table.ForeignKey(
                        name: "NegotiationOffers_jp_JobPostsId_fkey",
                        column: x => x.JobPostsId,
                        principalTable: "JobPosts",
                        principalColumn: "JobPostsId");
                    table.ForeignKey(
                        name: "NegotiationOffers_propo_ProposalsId_fkey",
                        column: x => x.ProposalsId,
                        principalTable: "Proposals",
                        principalColumn: "ProposalsId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ReplyToMessageId",
                table: "Messages",
                column: "ReplyToMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SenderUserId",
                table: "Messages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "Messages_conv_sender_client_key",
                table: "Messages",
                columns: new[] { "ConversationsId", "SenderUserId", "ClientMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_DisputesId",
                table: "Conversations",
                column: "DisputesId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_JobPostsId",
                table: "Conversations",
                column: "JobPostsId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_CreatedByUserId",
                table: "Conversations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_LastMessageId",
                table: "Conversations",
                column: "LastMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ProposalsId",
                table: "Conversations",
                column: "ProposalsId");

            migrationBuilder.CreateIndex(
                name: "ConversationParticipants_conv_User_key",
                table: "ConversationParticipants",
                columns: new[] { "ConversationsId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipants_ConversationsId",
                table: "ConversationParticipants",
                column: "ConversationsId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipants_LastReadMessageId",
                table: "ConversationParticipants",
                column: "LastReadMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipants_UserId",
                table: "ConversationParticipants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_NegotiationOffers_ClientProfilesId",
                table: "NegotiationOffers",
                column: "ClientProfilesId");

            migrationBuilder.CreateIndex(
                name: "IX_NegotiationOffers_ContractsId",
                table: "NegotiationOffers",
                column: "ContractsId");

            migrationBuilder.CreateIndex(
                name: "IX_NegotiationOffers_ConversationsId_Status",
                table: "NegotiationOffers",
                columns: new[] { "ConversationsId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_NegotiationOffers_FreelancerProfilesId",
                table: "NegotiationOffers",
                column: "FreelancerProfilesId");

            migrationBuilder.CreateIndex(
                name: "IX_NegotiationOffers_JobPostsId_Status",
                table: "NegotiationOffers",
                columns: new[] { "JobPostsId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_NegotiationOffers_ProposalsId",
                table: "NegotiationOffers",
                column: "ProposalsId");

            migrationBuilder.CreateIndex(
                name: "UX_NegotiationOffers_AcceptedPerJobPost",
                table: "NegotiationOffers",
                columns: new[] { "JobPostsId", "Status" },
                unique: true,
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "UX_NegotiationOffers_PendingPerConversation",
                table: "NegotiationOffers",
                columns: new[] { "ConversationsId", "Status" },
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.AddForeignKey(
                name: "Conversations_disp_DisputesId_fkey",
                table: "Conversations",
                column: "DisputesId",
                principalTable: "Disputes",
                principalColumn: "DisputesId");

            migrationBuilder.AddForeignKey(
                name: "Conversations_jp_JobPostsId_fkey",
                table: "Conversations",
                column: "JobPostsId",
                principalTable: "JobPosts",
                principalColumn: "JobPostsId");

            migrationBuilder.AddForeignKey(
                name: "Conversations_msg_LastMessageId_fkey",
                table: "Conversations",
                column: "LastMessageId",
                principalTable: "Messages",
                principalColumn: "MessagesId");

            migrationBuilder.AddForeignKey(
                name: "Conversations_propo_ProposalsId_fkey",
                table: "Conversations",
                column: "ProposalsId",
                principalTable: "Proposals",
                principalColumn: "ProposalsId");

            migrationBuilder.AddForeignKey(
                name: "Conversations_usr_CreatedByUserId_fkey",
                table: "Conversations",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "Messages_msg_ReplyToMessageId_fkey",
                table: "Messages",
                column: "ReplyToMessageId",
                principalTable: "Messages",
                principalColumn: "MessagesId");

            migrationBuilder.AddForeignKey(
                name: "Messages_usr_SenderUserId_fkey",
                table: "Messages",
                column: "SenderUserId",
                principalTable: "Users",
                principalColumn: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "Conversations_disp_DisputesId_fkey",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "Conversations_jp_JobPostsId_fkey",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "Conversations_msg_LastMessageId_fkey",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "Conversations_propo_ProposalsId_fkey",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "Conversations_usr_CreatedByUserId_fkey",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "Messages_msg_ReplyToMessageId_fkey",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "Messages_usr_SenderUserId_fkey",
                table: "Messages");

            migrationBuilder.DropTable(
                name: "ConversationParticipants");

            migrationBuilder.DropTable(
                name: "NegotiationOffers");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ReplyToMessageId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_SenderUserId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "Messages_conv_sender_client_key",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_DisputesId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_JobPostsId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_LastMessageId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_ProposalsId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ClientMessageId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "DeletedForEveryoneAt",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "DeletedForSenderAt",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "MessageType",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ReplyToMessageId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "SenderUserId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "FileExtension",
                table: "MessageAttachments");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "MessageAttachments");

            migrationBuilder.DropColumn(
                name: "MimeType",
                table: "MessageAttachments");

            migrationBuilder.DropColumn(
                name: "StorageObjectKey",
                table: "MessageAttachments");

            migrationBuilder.DropColumn(
                name: "StorageProvider",
                table: "MessageAttachments");

            migrationBuilder.DropColumn(
                name: "ConversationType",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "DisputesId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "JobPostsId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "LastMessageId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ProposalsId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Conversations");

            migrationBuilder.RenameColumn(
                name: "SentAt",
                table: "Messages",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "EditedAt",
                table: "Messages",
                newName: "UpdatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_Messages_ConversationsId_SentAt",
                table: "Messages",
                newName: "IX_Messages_ConversationsId_CreatedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedByUserId",
                table: "Conversations",
                newName: "User2Id");

            migrationBuilder.RenameIndex(
                name: "IX_Conversations_CreatedByUserId",
                table: "Conversations",
                newName: "IX_Conversations_User2Id");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Messages",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsEdited",
                table: "Messages",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "Messages",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SenderId",
                table: "Messages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Messages",
                type: "integer",
                nullable: true,
                defaultValue: 0,
                comment: "Enum MessageType: 0=Text, 1=File, 2=System");

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "MessageAttachments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                table: "MessageAttachments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Conversations",
                type: "integer",
                nullable: true,
                defaultValue: 0,
                comment: "Enum ConversationType: 0=DirectMessage, 1=ContractChat");

            migrationBuilder.AddColumn<Guid>(
                name: "User1Id",
                table: "Conversations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql(
                """
                UPDATE "Contracts"
                SET "Status" = CASE "Status"
                    WHEN 5 THEN 2
                    WHEN 6 THEN 3
                    WHEN 7 THEN 4
                    WHEN 8 THEN 5
                    WHEN 9 THEN 6
                    WHEN 10 THEN 7
                    ELSE "Status"
                END;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Contracts",
                type: "integer",
                nullable: false,
                comment: "Enum ContractStatus: 0=Draft, 1=PendingFreelancerSelection, 2=PendingEscrow, 3=PendingSignature, 4=Active, 5=Completed, 6=Cancelled, 7=Disputed",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum ContractStatus: 0=Draft, 1=PendingFreelancerSelection, 2=InNegotiation, 3=PendingContractDetails, 4=PendingContractConfirmation, 5=PendingEscrow, 6=PendingSignature, 7=Active, 8=Completed, 9=Cancelled, 10=Disputed");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_IsRead",
                table: "Messages",
                column: "IsRead");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SenderId",
                table: "Messages",
                column: "SenderId");

            migrationBuilder.AddUniqueConstraint(
                name: "Conversations_usr_User1Id_usr_User2Id_cont_ContractsId_key",
                table: "Conversations",
                columns: new[] { "User1Id", "User2Id", "ContractsId" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_User1Id",
                table: "Conversations",
                column: "User1Id");

            migrationBuilder.AddForeignKey(
                name: "Conversations_usr_User1Id_fkey",
                table: "Conversations",
                column: "User1Id",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "Conversations_usr_User2Id_fkey",
                table: "Conversations",
                column: "User2Id",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "Messages_usr_SenderId_fkey",
                table: "Messages",
                column: "SenderId",
                principalTable: "Users",
                principalColumn: "UserId");
        }
    }
}
