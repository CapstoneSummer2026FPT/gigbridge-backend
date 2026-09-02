using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemDeliveryState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContractWorkItemSubmissionId",
                table: "MilestoneAttachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "ContractWorkItems",
                type: "integer",
                nullable: false,
                comment: "Enum ContractWorkItemStatus: 0=Todo, 1=InProgress, 2=Completed (legacy), 3=RevisionRequired, 4=Submitted, 5=Approved",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                table: "ContractWorkItems",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryMode",
                table: "Contracts",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "Enum MilestoneDeliveryMode: 0=Legacy (milestone-level submit/approve), 1=WorkItem (per work item submit/approve)");

            migrationBuilder.CreateTable(
                name: "ContractWorkItemSubmissions",
                columns: table => new
                {
                    ContractWorkItemSubmissionId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ContractWorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    SubmissionBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    SubmittedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0, comment: "Enum ContractWorkItemSubmissionReviewStatus: 0=Submitted, 1=Approved, 2=RevisionRequired"),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractWorkItemSubmissions", x => x.ContractWorkItemSubmissionId);
                    table.ForeignKey(
                        name: "FK_ContractWorkItemSubmissions_ContractWorkItems_ContractWorkI~",
                        column: x => x.ContractWorkItemId,
                        principalTable: "ContractWorkItems",
                        principalColumn: "ContractWorkItemId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContractWorkItemSubmissions_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractWorkItemSubmissions_Users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MilestoneAttachments_ContractWorkItemSubmissionId",
                table: "MilestoneAttachments",
                column: "ContractWorkItemSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractWorkItemSubmissions_ContractWorkItemId_RevisionNumb~",
                table: "ContractWorkItemSubmissions",
                columns: new[] { "ContractWorkItemId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractWorkItemSubmissions_ContractWorkItemId_SubmissionBa~",
                table: "ContractWorkItemSubmissions",
                columns: new[] { "ContractWorkItemId", "SubmissionBatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractWorkItemSubmissions_ReviewedByUserId",
                table: "ContractWorkItemSubmissions",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractWorkItemSubmissions_SubmittedByUserId",
                table: "ContractWorkItemSubmissions",
                column: "SubmittedByUserId");

            migrationBuilder.AddForeignKey(
                name: "MilestoneAttachments_ContractWorkItemSubmissionId_fkey",
                table: "MilestoneAttachments",
                column: "ContractWorkItemSubmissionId",
                principalTable: "ContractWorkItemSubmissions",
                principalColumn: "ContractWorkItemSubmissionId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "MilestoneAttachments_ContractWorkItemSubmissionId_fkey",
                table: "MilestoneAttachments");

            migrationBuilder.DropTable(
                name: "ContractWorkItemSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_MilestoneAttachments_ContractWorkItemSubmissionId",
                table: "MilestoneAttachments");

            migrationBuilder.DropColumn(
                name: "ContractWorkItemSubmissionId",
                table: "MilestoneAttachments");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "ContractWorkItems");

            migrationBuilder.DropColumn(
                name: "DeliveryMode",
                table: "Contracts");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "ContractWorkItems",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum ContractWorkItemStatus: 0=Todo, 1=InProgress, 2=Completed (legacy), 3=RevisionRequired, 4=Submitted, 5=Approved");
        }
    }
}
