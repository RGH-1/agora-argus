using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Argus.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InMaintenance",
                table: "ClientApps",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MaintenanceMessage",
                table: "ClientApps",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "ClientApps",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "InMaintenance", "MaintenanceMessage" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "ClientApps",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "InMaintenance", "MaintenanceMessage" },
                values: new object[] { false, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InMaintenance",
                table: "ClientApps");

            migrationBuilder.DropColumn(
                name: "MaintenanceMessage",
                table: "ClientApps");
        }
    }
}
