using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agora.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitializeMaintenanceStateSingleton : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The original AA-46 migration made Id an IDENTITY column.  The runtime singleton uses Id=1,
            // so initialize that exact row here rather than asking EF to insert an explicit identity value.
            // If an operator/database already has a state row under a different identity, copy its newest
            // values to the singleton key and retain the original row; no recorded state is overwritten.
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [MaintenanceStates] WHERE [Id] = 1)
                BEGIN
                    SET IDENTITY_INSERT [MaintenanceStates] ON;

                    IF EXISTS (SELECT 1 FROM [MaintenanceStates])
                    BEGIN
                        INSERT INTO [MaintenanceStates] ([Id], [IsEnabled], [LastEventOccurredAtUtc], [Message], [UpdatedAtUtc])
                        SELECT TOP (1) 1, [IsEnabled], [LastEventOccurredAtUtc], [Message], [UpdatedAtUtc]
                        FROM [MaintenanceStates]
                        ORDER BY [UpdatedAtUtc] DESC, [Id] DESC;
                    END
                    ELSE
                    BEGIN
                        INSERT INTO [MaintenanceStates] ([Id], [IsEnabled], [LastEventOccurredAtUtc], [Message], [UpdatedAtUtc])
                        VALUES (1, 0, NULL, NULL, TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'));
                    END

                    SET IDENTITY_INSERT [MaintenanceStates] OFF;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately retain the durable maintenance state during rollback.
        }
    }
}
