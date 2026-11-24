using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agora.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRuleSeedState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RuleSeedStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InitialRulesSeededAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleSeedStates", x => x.Id);
                });

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [Rules]) OR EXISTS (SELECT 1 FROM [AspNetUsers])
                BEGIN
                    SET IDENTITY_INSERT [RuleSeedStates] ON;
                    INSERT INTO [RuleSeedStates] ([Id], [InitialRulesSeededAtUtc]) VALUES (1, SYSUTCDATETIME());
                    SET IDENTITY_INSERT [RuleSeedStates] OFF;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RuleSeedStates");
        }
    }
}
