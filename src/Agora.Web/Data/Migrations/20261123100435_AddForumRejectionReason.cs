using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agora.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddForumRejectionReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Forums",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Forums");
        }
    }
}
