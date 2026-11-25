using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agora.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserBanActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserBanActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TargetUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsBan = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBanActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserBanActions_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserBanActions_AspNetUsers_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserBanActions_ActorId",
                table: "UserBanActions",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBanActions_TargetUserId_CreatedAtUtc",
                table: "UserBanActions",
                columns: new[] { "TargetUserId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserBanActions");
        }
    }
}
