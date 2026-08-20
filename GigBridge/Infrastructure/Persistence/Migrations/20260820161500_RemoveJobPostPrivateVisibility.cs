using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

[DbContext(typeof(GigbridgeDbContext))]
[Migration("20260820161500_RemoveJobPostPrivateVisibility")]
public partial class RemoveJobPostPrivateVisibility : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "UPDATE \"JobPosts\" SET \"Visibility\" = 0 WHERE \"Visibility\" = 1;");

        migrationBuilder.AlterColumn<int>(
            name: "Visibility",
            table: "JobPosts",
            type: "integer",
            nullable: true,
            defaultValue: 0,
            comment: "Enum JobPostVisibility: 0=Public, 2=InviteOnly",
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true,
            oldDefaultValue: 0,
            oldComment: "Enum JobPostVisibility: 0=Public, 1=Private, 2=InviteOnly");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<int>(
            name: "Visibility",
            table: "JobPosts",
            type: "integer",
            nullable: true,
            defaultValue: 0,
            comment: "Enum JobPostVisibility: 0=Public, 1=Private, 2=InviteOnly",
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true,
            oldDefaultValue: 0,
            oldComment: "Enum JobPostVisibility: 0=Public, 2=InviteOnly");
    }
}
