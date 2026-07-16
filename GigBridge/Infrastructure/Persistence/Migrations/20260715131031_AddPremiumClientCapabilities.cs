using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPremiumClientCapabilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FeaturedFrom",
                table: "JobPosts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FeaturedUntil",
                table: "JobPosts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFeatured",
                table: "JobPosts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AiAnalysisStatus",
                table: "Disputes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AiSuggestedResolution",
                table: "Disputes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVipPriority",
                table: "Disputes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolutionTargetAt",
                table: "Disputes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiInterviewDefinitions",
                columns: table => new
                {
                    AiInterviewDefinitionsId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPostId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    QuestionCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiInterviewDefinitions", x => x.AiInterviewDefinitionsId);
                    table.ForeignKey(
                        name: "FK_AiInterviewDefinitions_JobPosts_JobPostId",
                        column: x => x.JobPostId,
                        principalTable: "JobPosts",
                        principalColumn: "JobPostsId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiInterviewDefinitions_Users_ClientUserId",
                        column: x => x.ClientUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobPostPromotions",
                columns: table => new
                {
                    JobPostPromotionsId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPostId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TokenCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    FeaturedFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FeaturedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPostPromotions", x => x.JobPostPromotionsId);
                    table.ForeignKey(
                        name: "FK_JobPostPromotions_JobPosts_JobPostId",
                        column: x => x.JobPostId,
                        principalTable: "JobPosts",
                        principalColumn: "JobPostsId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobPostPromotions_Users_ClientUserId",
                        column: x => x.ClientUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobPostPromotions_WalletTransactions_WalletTransactionId",
                        column: x => x.WalletTransactionId,
                        principalTable: "WalletTransactions",
                        principalColumn: "WalletTransactionsId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiInterviewAttempts",
                columns: table => new
                {
                    AiInterviewAttemptsId = table.Column<Guid>(type: "uuid", nullable: false),
                    AiInterviewDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FreelancerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalSessionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OverallScore = table.Column<int>(type: "integer", nullable: true),
                    CompatibilityScore = table.Column<int>(type: "integer", nullable: true),
                    EvaluationSummary = table.Column<string>(type: "text", nullable: true),
                    TechnicalSkillsJson = table.Column<string>(type: "text", nullable: true),
                    SoftSkillsJson = table.Column<string>(type: "text", nullable: true),
                    RecommendedHire = table.Column<bool>(type: "boolean", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiInterviewAttempts", x => x.AiInterviewAttemptsId);
                    table.ForeignKey(
                        name: "FK_AiInterviewAttempts_AiInterviewDefinitions_AiInterviewDefin~",
                        column: x => x.AiInterviewDefinitionId,
                        principalTable: "AiInterviewDefinitions",
                        principalColumn: "AiInterviewDefinitionsId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiInterviewAttempts_Users_FreelancerUserId",
                        column: x => x.FreelancerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiInterviewAnswerResults",
                columns: table => new
                {
                    AiInterviewAnswerResultsId = table.Column<Guid>(type: "uuid", nullable: false),
                    AiInterviewAttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionIndex = table.Column<int>(type: "integer", nullable: false),
                    QuestionText = table.Column<string>(type: "text", nullable: true),
                    Transcript = table.Column<string>(type: "text", nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiInterviewAnswerResults", x => x.AiInterviewAnswerResultsId);
                    table.ForeignKey(
                        name: "FK_AiInterviewAnswerResults_AiInterviewAttempts_AiInterviewAtt~",
                        column: x => x.AiInterviewAttemptId,
                        principalTable: "AiInterviewAttempts",
                        principalColumn: "AiInterviewAttemptsId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobPosts_IsFeatured_FeaturedUntil",
                table: "JobPosts",
                columns: new[] { "IsFeatured", "FeaturedUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_Status_Vip_ResolutionTarget",
                table: "Disputes",
                columns: new[] { "Status", "IsVipPriority", "ResolutionTargetAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiInterviewAnswerResults_AiInterviewAttemptId_QuestionIndex",
                table: "AiInterviewAnswerResults",
                columns: new[] { "AiInterviewAttemptId", "QuestionIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiInterviewAttempts_AiInterviewDefinitionId_Status",
                table: "AiInterviewAttempts",
                columns: new[] { "AiInterviewDefinitionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AiInterviewAttempts_ExternalSessionId",
                table: "AiInterviewAttempts",
                column: "ExternalSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiInterviewAttempts_FreelancerUserId",
                table: "AiInterviewAttempts",
                column: "FreelancerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AiInterviewDefinitions_ClientUserId_JobPostId_Status",
                table: "AiInterviewDefinitions",
                columns: new[] { "ClientUserId", "JobPostId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AiInterviewDefinitions_JobPostId",
                table: "AiInterviewDefinitions",
                column: "JobPostId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPostPromotions_ClientUserId_IdempotencyKey",
                table: "JobPostPromotions",
                columns: new[] { "ClientUserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobPostPromotions_JobPostId_FeaturedUntil",
                table: "JobPostPromotions",
                columns: new[] { "JobPostId", "FeaturedUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_JobPostPromotions_WalletTransactionId",
                table: "JobPostPromotions",
                column: "WalletTransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiInterviewAnswerResults");

            migrationBuilder.DropTable(
                name: "JobPostPromotions");

            migrationBuilder.DropTable(
                name: "AiInterviewAttempts");

            migrationBuilder.DropTable(
                name: "AiInterviewDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_JobPosts_IsFeatured_FeaturedUntil",
                table: "JobPosts");

            migrationBuilder.DropIndex(
                name: "IX_Disputes_Status_Vip_ResolutionTarget",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "FeaturedFrom",
                table: "JobPosts");

            migrationBuilder.DropColumn(
                name: "FeaturedUntil",
                table: "JobPosts");

            migrationBuilder.DropColumn(
                name: "IsFeatured",
                table: "JobPosts");

            migrationBuilder.DropColumn(
                name: "AiAnalysisStatus",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "AiSuggestedResolution",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "IsVipPriority",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "ResolutionTargetAt",
                table: "Disputes");
        }
    }
}
