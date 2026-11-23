using Agora.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agora.Web.Data.Migrations;

[DbContext(typeof(AgoraDbContext))]
[Migration("20261123120000_AddForumSubmittedAt")]
public partial class AddForumSubmittedAt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "SubmittedAtUtc",
            table: "Forums",
            type: "datetime2",
            nullable: true);

        migrationBuilder.Sql("UPDATE [Forums] SET [SubmittedAtUtc] = [CreatedAtUtc]");

        migrationBuilder.AlterColumn<DateTime>(
            name: "SubmittedAtUtc",
            table: "Forums",
            type: "datetime2",
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "datetime2",
            oldNullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "SubmittedAtUtc", table: "Forums");
    }
}
