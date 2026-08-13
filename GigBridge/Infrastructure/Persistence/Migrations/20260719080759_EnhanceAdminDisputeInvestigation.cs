using Domain.Enums.Disputes;
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceAdminDisputeInvestigation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "DisputeEvidence_usr_UploadedById_fkey",
                table: "DisputeEvidence");

            migrationBuilder.AlterColumn<Guid>(
                name: "UploadedById",
                table: "DisputeEvidence",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "FileUrl",
                table: "DisputeEvidence",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "DisputeEvidence",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<DateTime>(
                name: "Deadline",
                table: "DisputeEvidence",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRequestFulfilled",
                table: "DisputeEvidence",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRequestedByAdmin",
                table: "DisputeEvidence",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestGroupId",
                table: "DisputeEvidence",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequestTarget",
                table: "DisputeEvidence",
                type: "integer",
                nullable: true,
                comment: "Enum EvidenceRequestTarget: 0=Reporter, 1=Respondent, 2=Both");

            migrationBuilder.AddColumn<DateTime>(
                name: "RequestedAt",
                table: "DisputeEvidence",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedByAdminId",
                table: "DisputeEvidence",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "DisputeEvidence",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "DisputeEvidence",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByAdminId",
                table: "DisputeEvidence",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DisputeMilestoneDecisions",
                columns: table => new
                {
                    DisputeMilestoneDecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisputesId = table.Column<Guid>(type: "uuid", nullable: false),
                    MilestonesId = table.Column<Guid>(type: "uuid", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false, comment: "Enum DisputeMilestoneOutcome: 0=Accepted, 1=Rejected, 2=PartiallyAccepted, 3=Cancelled"),
                    MilestoneAmountSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReleasedAmountSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AdditionalReleaseAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DecidedByAdminId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisputeMilestoneDecisions", x => x.DisputeMilestoneDecisionId);
                    table.ForeignKey(
                        name: "FK_DisputeMilestoneDecisions_Disputes_DisputesId",
                        column: x => x.DisputesId,
                        principalTable: "Disputes",
                        principalColumn: "DisputesId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DisputeMilestoneDecisions_Milestones_MilestonesId",
                        column: x => x.MilestonesId,
                        principalTable: "Milestones",
                        principalColumn: "MilestonesId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DisputeMilestoneDecisions_Users_DecidedByAdminId",
                        column: x => x.DecidedByAdminId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DisputeEvidence_DisputesId_RequestGroupId",
                table: "DisputeEvidence",
                columns: new[] { "DisputesId", "RequestGroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_DisputeEvidence_RequestedByAdminId",
                table: "DisputeEvidence",
                column: "RequestedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_DisputeEvidence_ReviewedByAdminId",
                table: "DisputeEvidence",
                column: "ReviewedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_DisputeMilestoneDecisions_DecidedByAdminId",
                table: "DisputeMilestoneDecisions",
                column: "DecidedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_DisputeMilestoneDecisions_DisputesId_MilestonesId",
                table: "DisputeMilestoneDecisions",
                columns: new[] { "DisputesId", "MilestonesId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DisputeMilestoneDecisions_MilestonesId",
                table: "DisputeMilestoneDecisions",
                column: "MilestonesId");

            migrationBuilder.AddForeignKey(
                name: "DisputeEvidence_RequestedByAdminId_fkey",
                table: "DisputeEvidence",
                column: "RequestedByAdminId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "DisputeEvidence_ReviewedByAdminId_fkey",
                table: "DisputeEvidence",
                column: "ReviewedByAdminId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "DisputeEvidence_usr_UploadedById_fkey",
                table: "DisputeEvidence",
                column: "UploadedById",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "DisputeEvidence_RequestedByAdminId_fkey",
                table: "DisputeEvidence");

            migrationBuilder.DropForeignKey(
                name: "DisputeEvidence_ReviewedByAdminId_fkey",
                table: "DisputeEvidence");

            migrationBuilder.DropForeignKey(
                name: "DisputeEvidence_usr_UploadedById_fkey",
                table: "DisputeEvidence");

            migrationBuilder.DropTable(
                name: "DisputeMilestoneDecisions");

            migrationBuilder.DropIndex(
                name: "IX_DisputeEvidence_DisputesId_RequestGroupId",
                table: "DisputeEvidence");

            migrationBuilder.DropIndex(
                name: "IX_DisputeEvidence_RequestedByAdminId",
                table: "DisputeEvidence");

            migrationBuilder.DropIndex(
                name: "IX_DisputeEvidence_ReviewedByAdminId",
                table: "DisputeEvidence");

            migrationBuilder.DropColumn(
                name: "Deadline",
                table: "DisputeEvidence");

            migrationBuilder.DropColumn(
                name: "IsRequestFulfilled",
                table: "DisputeEvidence");

            migrationBuilder.DropColumn(
                name: "IsRequestedByAdmin",
                table: "DisputeEvidence");

            migrationBuilder.DropColumn(
                name: "RequestGroupId",
                table: "DisputeEvidence");

            migrationBuilder.DropColumn(
                name: "RequestTarget",
                table: "DisputeEvidence");

            migrationBuilder.DropColumn(
                name: "RequestedAt",
                table: "DisputeEvidence");

            migrationBuilder.DropColumn(
                name: "RequestedByAdminId",
                table: "DisputeEvidence");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "DisputeEvidence");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "DisputeEvidence");

            migrationBuilder.DropColumn(
                name: "ReviewedByAdminId",
                table: "DisputeEvidence");

            migrationBuilder.AlterColumn<Guid>(
                name: "UploadedById",
                table: "DisputeEvidence",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FileUrl",
                table: "DisputeEvidence",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "DisputeEvidence",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "DisputeEvidence_usr_UploadedById_fkey",
                table: "DisputeEvidence",
                column: "UploadedById",
                principalTable: "Users",
                principalColumn: "UserId");
        }
    }
}
