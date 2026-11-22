using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agora.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddForumNameUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Forums",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE [Forums] SET [NormalizedName] = UPPER(LTRIM(RTRIM([Name])))");

            migrationBuilder.CreateIndex(
                name: "IX_Forums_NormalizedName",
                table: "Forums",
                column: "NormalizedName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Forums_NormalizedName",
                table: "Forums");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Forums");
        }
    }
}
