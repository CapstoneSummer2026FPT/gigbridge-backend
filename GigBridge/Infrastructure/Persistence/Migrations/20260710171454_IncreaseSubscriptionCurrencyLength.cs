using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IncreaseSubscriptionCurrencyLength : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "ContractEscrows",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "'VND'::character varying",
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5,
                oldDefaultValueSql: "'VND'::character varying");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "ContractEscrows",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                defaultValueSql: "'VND'::character varying",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValueSql: "'VND'::character varying");
        }
    }
}
