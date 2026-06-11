using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobPostQuestionProposalAnswerApiConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobPostQuestions",
                columns: table => new
                {
                    JobPostQuestionsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    JobPostsId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("JobPostQuestions_pkey", x => x.JobPostQuestionsId);
                    table.ForeignKey(
                        name: "JobPostQuestions_jp_JobPostsId_fkey",
                        column: x => x.JobPostsId,
                        principalTable: "JobPosts",
                        principalColumn: "JobPostsId");
                });

            migrationBuilder.CreateTable(
                name: "ProposalAnswers",
                columns: table => new
                {
                    ProposalAnswersId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ProposalsId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPostQuestionsId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnswerText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ProposalAnswers_pkey", x => x.ProposalAnswersId);
                    table.ForeignKey(
                        name: "ProposalAnswers_jpq_JobPostQuestionsId_fkey",
                        column: x => x.JobPostQuestionsId,
                        principalTable: "JobPostQuestions",
                        principalColumn: "JobPostQuestionsId");
                    table.ForeignKey(
                        name: "ProposalAnswers_propo_ProposalsId_fkey",
                        column: x => x.ProposalsId,
                        principalTable: "Proposals",
                        principalColumn: "ProposalsId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobPostQuestions_JobPostsId",
                table: "JobPostQuestions",
                column: "JobPostsId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPostQuestions_JobPostsId_OrderIndex",
                table: "JobPostQuestions",
                columns: new[] { "JobPostsId", "OrderIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProposalAnswers_JobPostQuestionsId",
                table: "ProposalAnswers",
                column: "JobPostQuestionsId");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalAnswers_ProposalsId",
                table: "ProposalAnswers",
                column: "ProposalsId");

            migrationBuilder.CreateIndex(
                name: "ProposalAnswers_propo_ProposalsId_jpq_JobPostQuestionsId_key",
                table: "ProposalAnswers",
                columns: new[] { "ProposalsId", "JobPostQuestionsId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProposalAnswers");

            migrationBuilder.DropTable(
                name: "JobPostQuestions");
        }
    }
}
