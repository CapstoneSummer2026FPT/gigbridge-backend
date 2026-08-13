using Domain.Enums.Disputes;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDisputeMessageRecipientTargeting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DisputeRecipient",
                table: "Messages",
                type: "integer",
                nullable: true,
                comment: "Enum DisputeMessageRecipient: 0=Client, 1=Freelancer, 2=Both; null=non-dispute or legacy shared");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Messages_DisputeRecipient_Valid",
                table: "Messages",
                sql: "\"DisputeRecipient\" IS NULL OR \"DisputeRecipient\" BETWEEN 0 AND 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Messages_DisputeRecipient_Valid",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "DisputeRecipient",
                table: "Messages");
        }
    }
}
