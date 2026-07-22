using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiTalentMatchingV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TalentMatchRuns",
                columns: table => new
                {
                    TalentMatchRunId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ClientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPostId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EmbeddingModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LlmModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PromptVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EligibleCandidateCount = table.Column<int>(type: "integer", nullable: false),
                    ReturnedCandidateCount = table.Column<int>(type: "integer", nullable: false),
                    LatencyMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Enum TalentMatchRunStatus: 0=Running, 1=Succeeded, 2=NoCandidates, 3=Failed"),
                    CacheHit = table.Column<bool>(type: "boolean", nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TalentMatchRuns", x => x.TalentMatchRunId);
                    table.ForeignKey(
                        name: "FK_TalentMatchRuns_JobPosts_JobPostId",
                        column: x => x.JobPostId,
                        principalTable: "JobPosts",
                        principalColumn: "JobPostsId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TalentMatchRuns_Users_ClientUserId",
                        column: x => x.ClientUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TalentMatchResults",
                columns: table => new
                {
                    TalentMatchResultId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TalentMatchRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    FreelancerProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    EmbeddingScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    LlmScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    EvidenceScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    FinalScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Confidence = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MatchedSkills = table.Column<string[]>(type: "text[]", nullable: false),
                    MissingSkills = table.Column<string[]>(type: "text[]", nullable: false),
                    SemanticStrengths = table.Column<string[]>(type: "text[]", nullable: false),
                    Reasons = table.Column<string[]>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TalentMatchResults", x => x.TalentMatchResultId);
                    table.UniqueConstraint("AK_TalentMatchResults_TalentMatchRunId_FreelancerProfileId", x => new { x.TalentMatchRunId, x.FreelancerProfileId });
                    table.ForeignKey(
                        name: "FK_TalentMatchResults_FreelancerProfiles_FreelancerProfileId",
                        column: x => x.FreelancerProfileId,
                        principalTable: "FreelancerProfiles",
                        principalColumn: "FreelancerProfilesId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TalentMatchResults_TalentMatchRuns_TalentMatchRunId",
                        column: x => x.TalentMatchRunId,
                        principalTable: "TalentMatchRuns",
                        principalColumn: "TalentMatchRunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TalentMatchEvents",
                columns: table => new
                {
                    TalentMatchEventId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TalentMatchRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    FreelancerProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false, comment: "Enum TalentMatchEventType: 0=Impression, 1=ProfileOpened, 2=Saved, 3=Invited, 4=ProposalSubmitted, 5=Shortlisted, 6=InterviewStarted, 7=InterviewCompleted, 8=Hired, 9=ContractCompleted"),
                    SourceEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TalentMatchEvents", x => x.TalentMatchEventId);
                    table.ForeignKey(
                        name: "FK_TalentMatchEvents_FreelancerProfiles_FreelancerProfileId",
                        column: x => x.FreelancerProfileId,
                        principalTable: "FreelancerProfiles",
                        principalColumn: "FreelancerProfilesId");
                    table.ForeignKey(
                        name: "FK_TalentMatchEvents_TalentMatchResults_TalentMatchRunId_Freel~",
                        columns: x => new { x.TalentMatchRunId, x.FreelancerProfileId },
                        principalTable: "TalentMatchResults",
                        principalColumns: new[] { "TalentMatchRunId", "FreelancerProfileId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TalentMatchEvents_TalentMatchRuns_TalentMatchRunId",
                        column: x => x.TalentMatchRunId,
                        principalTable: "TalentMatchRuns",
                        principalColumn: "TalentMatchRunId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TalentMatchEvents_FreelancerProfileId",
                table: "TalentMatchEvents",
                column: "FreelancerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_TalentMatchEvents_Run_Type_CreatedAt",
                table: "TalentMatchEvents",
                columns: new[] { "TalentMatchRunId", "EventType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TalentMatchEvents_TalentMatchRunId_FreelancerProfileId",
                table: "TalentMatchEvents",
                columns: new[] { "TalentMatchRunId", "FreelancerProfileId" });

            migrationBuilder.CreateIndex(
                name: "UX_TalentMatchEvents_IdempotencyKey",
                table: "TalentMatchEvents",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TalentMatchResults_FreelancerProfileId",
                table: "TalentMatchResults",
                column: "FreelancerProfileId");

            migrationBuilder.CreateIndex(
                name: "UX_TalentMatchResults_Run_Freelancer",
                table: "TalentMatchResults",
                columns: new[] { "TalentMatchRunId", "FreelancerProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TalentMatchRuns_ClientUserId_JobPostId_CreatedAt",
                table: "TalentMatchRuns",
                columns: new[] { "ClientUserId", "JobPostId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TalentMatchRuns_JobPostId",
                table: "TalentMatchRuns",
                column: "JobPostId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TalentMatchEvents");

            migrationBuilder.DropTable(
                name: "TalentMatchResults");

            migrationBuilder.DropTable(
                name: "TalentMatchRuns");

        }
    }
}
