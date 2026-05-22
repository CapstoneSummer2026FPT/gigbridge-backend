using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeleteUnnessaryEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "PortfolioItems_CategoryId_fkey",
                table: "PortfolioItems");

            migrationBuilder.DropTable(
                name: "AIInterviewQuestions");

            migrationBuilder.DropTable(
                name: "AIMessages");

            migrationBuilder.DropTable(
                name: "Certifications");

            migrationBuilder.DropTable(
                name: "Educations");

            migrationBuilder.DropTable(
                name: "ESignAuditTrails");

            migrationBuilder.DropTable(
                name: "AIInterviewSessions");

            migrationBuilder.DropTable(
                name: "AIConversationSessions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "PortfolioItems");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "PortfolioItems");

            migrationBuilder.DropColumn(
                name: "ImageUrls",
                table: "PortfolioItems");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "PortfolioItems");

            migrationBuilder.RenameColumn(
                name: "CategoryId",
                table: "PortfolioItems",
                newName: "CategoryCategoriesId");

            migrationBuilder.RenameIndex(
                name: "IX_PortfolioItems_CategoryId",
                table: "PortfolioItems",
                newName: "IX_PortfolioItems_CategoryCategoriesId");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "Users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PortfolioItems_Categories_CategoryCategoriesId",
                table: "PortfolioItems",
                column: "CategoryCategoriesId",
                principalTable: "Categories",
                principalColumn: "CategoriesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PortfolioItems_Categories_CategoryCategoriesId",
                table: "PortfolioItems");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Password",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "CategoryCategoriesId",
                table: "PortfolioItems",
                newName: "CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_PortfolioItems_CategoryCategoriesId",
                table: "PortfolioItems",
                newName: "IX_PortfolioItems_CategoryId");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "PortfolioItems",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "PortfolioItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrls",
                table: "PortfolioItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "PortfolioItems",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AIConversationSessions",
                columns: table => new
                {
                    AIConversationSessionsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ContractsId = table.Column<Guid>(type: "uuid", nullable: true),
                    JobPostsId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    ModelUsed = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TotalTokensUsed = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    Type = table.Column<int>(type: "integer", nullable: false, comment: "Enum AISessionType: 0=WorkAssistant, 1=ProfileOptimizer, 2=JobPostGenerator, 3=ProposalGenerator"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("AIConversationSessions_pkey", x => x.AIConversationSessionsId);
                    table.ForeignKey(
                        name: "AIConversationSessions_cont_ContractsId_fkey",
                        column: x => x.ContractsId,
                        principalTable: "Contracts",
                        principalColumn: "ContractsId");
                    table.ForeignKey(
                        name: "AIConversationSessions_jp_JobPostsId_fkey",
                        column: x => x.JobPostsId,
                        principalTable: "JobPosts",
                        principalColumn: "JobPostsId");
                    table.ForeignKey(
                        name: "AIConversationSessions_usr_UserId_fkey",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "AIInterviewSessions",
                columns: table => new
                {
                    AIInterviewSessionsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ClientProfilesId = table.Column<Guid>(type: "uuid", nullable: false),
                    FreelancerProfilesId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPostsId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalsId = table.Column<Guid>(type: "uuid", nullable: true),
                    AIFeedback = table.Column<string>(type: "text", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    OverallScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Enum InterviewStatus: 0=Pending, 1=InProgress, 2=Completed, 3=Cancelled"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("AIInterviewSessions_pkey", x => x.AIInterviewSessionsId);
                    table.ForeignKey(
                        name: "AIInterviewSessions_clPro_ClientProfilesId_fkey",
                        column: x => x.ClientProfilesId,
                        principalTable: "ClientProfiles",
                        principalColumn: "ClientProfilesId");
                    table.ForeignKey(
                        name: "AIInterviewSessions_flPro_FreelancerProfilesId_fkey",
                        column: x => x.FreelancerProfilesId,
                        principalTable: "FreelancerProfiles",
                        principalColumn: "FreelancerProfilesId");
                    table.ForeignKey(
                        name: "AIInterviewSessions_jp_JobPostsId_fkey",
                        column: x => x.JobPostsId,
                        principalTable: "JobPosts",
                        principalColumn: "JobPostsId");
                    table.ForeignKey(
                        name: "AIInterviewSessions_propo_ProposalsId_fkey",
                        column: x => x.ProposalsId,
                        principalTable: "Proposals",
                        principalColumn: "ProposalsId");
                });

            migrationBuilder.CreateTable(
                name: "Certifications",
                columns: table => new
                {
                    CertificationsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FreelancerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialUrl = table.Column<string>(type: "text", nullable: true),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IssuingOrganization = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("Certifications_pkey", x => x.CertificationsId);
                    table.ForeignKey(
                        name: "Certifications_fl_FreelancerId_fkey",
                        column: x => x.FreelancerId,
                        principalTable: "FreelancerProfiles",
                        principalColumn: "FreelancerProfilesId");
                });

            migrationBuilder.CreateTable(
                name: "Educations",
                columns: table => new
                {
                    EducationsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FreelancerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Degree = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FieldOfStudy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Institution = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("Educations_pkey", x => x.EducationsId);
                    table.ForeignKey(
                        name: "Educations_fl_FreelancerId_fkey",
                        column: x => x.FreelancerId,
                        principalTable: "FreelancerProfiles",
                        principalColumn: "FreelancerProfilesId");
                });

            migrationBuilder.CreateTable(
                name: "ESignAuditTrails",
                columns: table => new
                {
                    ESignAuditTrailsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ESignDocumentsId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false, comment: "Enum ESignAuditAction: 0=DocumentCreated, 1=DocumentViewed, 2=SignatureAdded, 3=SignatureDeclined, 4=DocumentFinalized, 5=DocumentExported, 6=DocumentVoided"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ESignAuditTrails_pkey", x => x.ESignAuditTrailsId);
                    table.ForeignKey(
                        name: "ESignAuditTrails_eDoc_ESignDocumentsId_fkey",
                        column: x => x.ESignDocumentsId,
                        principalTable: "ESignDocuments",
                        principalColumn: "ESignDocumentsId");
                    table.ForeignKey(
                        name: "ESignAuditTrails_usr_UserId_fkey",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "AIMessages",
                columns: table => new
                {
                    AIMessagesId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    AIConversationSessionsId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    TokensUsed = table.Column<int>(type: "integer", nullable: true, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("AIMessages_pkey", x => x.AIMessagesId);
                    table.ForeignKey(
                        name: "AIMessages_aiSess_AIConversationSessionsId_fkey",
                        column: x => x.AIConversationSessionsId,
                        principalTable: "AIConversationSessions",
                        principalColumn: "AIConversationSessionsId");
                });

            migrationBuilder.CreateTable(
                name: "AIInterviewQuestions",
                columns: table => new
                {
                    AIInterviewQuestionsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    AIInterviewSessionsId = table.Column<Guid>(type: "uuid", nullable: false),
                    AIAnalysis = table.Column<string>(type: "text", nullable: true),
                    Answer = table.Column<string>(type: "text", nullable: true),
                    AnsweredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    Question = table.Column<string>(type: "text", nullable: false),
                    Score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: true, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("AIInterviewQuestions_pkey", x => x.AIInterviewQuestionsId);
                    table.ForeignKey(
                        name: "AIInterviewQuestions_aiIntv_AIInterviewSessionsId_fkey",
                        column: x => x.AIInterviewSessionsId,
                        principalTable: "AIInterviewSessions",
                        principalColumn: "AIInterviewSessionsId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIConversationSessions_ContractsId",
                table: "AIConversationSessions",
                column: "ContractsId");

            migrationBuilder.CreateIndex(
                name: "IX_AIConversationSessions_JobPostsId",
                table: "AIConversationSessions",
                column: "JobPostsId");

            migrationBuilder.CreateIndex(
                name: "IX_AIConversationSessions_Type",
                table: "AIConversationSessions",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_AIConversationSessions_UserId",
                table: "AIConversationSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AIConversationSessions_UserId_Type",
                table: "AIConversationSessions",
                columns: new[] { "UserId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_AIInterviewQuestions_SessionId_SortOrder",
                table: "AIInterviewQuestions",
                columns: new[] { "AIInterviewSessionsId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "AIInterviewSessions_jp_JobPostsId_flPro_FreelancerProfilesI_key",
                table: "AIInterviewSessions",
                columns: new[] { "JobPostsId", "FreelancerProfilesId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AIInterviewSessions_ClientProfilesId",
                table: "AIInterviewSessions",
                column: "ClientProfilesId");

            migrationBuilder.CreateIndex(
                name: "IX_AIInterviewSessions_FreelancerProfilesId",
                table: "AIInterviewSessions",
                column: "FreelancerProfilesId");

            migrationBuilder.CreateIndex(
                name: "IX_AIInterviewSessions_JobPostsId",
                table: "AIInterviewSessions",
                column: "JobPostsId");

            migrationBuilder.CreateIndex(
                name: "IX_AIInterviewSessions_ProposalsId",
                table: "AIInterviewSessions",
                column: "ProposalsId");

            migrationBuilder.CreateIndex(
                name: "IX_AIInterviewSessions_Status",
                table: "AIInterviewSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AIMessages_SessionId_SortOrder",
                table: "AIMessages",
                columns: new[] { "AIConversationSessionsId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Certifications_FreelancerId",
                table: "Certifications",
                column: "FreelancerId");

            migrationBuilder.CreateIndex(
                name: "IX_Educations_FreelancerId",
                table: "Educations",
                column: "FreelancerId");

            migrationBuilder.CreateIndex(
                name: "IX_ESignAuditTrails_Action",
                table: "ESignAuditTrails",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_ESignAuditTrails_DocId_CreatedAt",
                table: "ESignAuditTrails",
                columns: new[] { "ESignDocumentsId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ESignAuditTrails_UserId",
                table: "ESignAuditTrails",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "PortfolioItems_CategoryId_fkey",
                table: "PortfolioItems",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "CategoriesId");
        }
    }
}
