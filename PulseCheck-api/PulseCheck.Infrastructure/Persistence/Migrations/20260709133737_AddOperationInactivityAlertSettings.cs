using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulseCheck.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationInactivityAlertSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[ClientInactivityAlertSettings]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'ClientInactivityAlertSettings', N'Operation') IS NULL
                BEGIN
                    ALTER TABLE [ClientInactivityAlertSettings]
                    ADD [Operation] nvarchar(180) NOT NULL CONSTRAINT [DF_ClientInactivityAlertSettings_Operation] DEFAULT N'';
                END;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[ClientInactivityAlertSettings]', N'U') IS NOT NULL
                   AND EXISTS (
                       SELECT 1
                       FROM sys.indexes
                       WHERE name = N'IX_ClientInactivityAlertSettings_Client_AlertThresholdMinutes'
                         AND object_id = OBJECT_ID(N'[ClientInactivityAlertSettings]')
                   )
                BEGIN
                    DROP INDEX [IX_ClientInactivityAlertSettings_Client_AlertThresholdMinutes]
                    ON [ClientInactivityAlertSettings];
                END;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[ClientInactivityAlertSettings]', N'U') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1
                       FROM sys.indexes
                       WHERE name = N'IX_ClientInactivityAlertSettings_Operation'
                         AND object_id = OBJECT_ID(N'[ClientInactivityAlertSettings]')
                   )
                BEGIN
                    CREATE INDEX [IX_ClientInactivityAlertSettings_Operation]
                    ON [ClientInactivityAlertSettings] ([Operation]);
                END;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[ClientInactivityAlertSettings]', N'U') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1
                       FROM sys.indexes
                       WHERE name = N'IX_ClientInactivityAlertSettings_Client_Operation_AlertThresholdMinutes'
                         AND object_id = OBJECT_ID(N'[ClientInactivityAlertSettings]')
                   )
                BEGIN
                    CREATE UNIQUE INDEX [IX_ClientInactivityAlertSettings_Client_Operation_AlertThresholdMinutes]
                    ON [ClientInactivityAlertSettings] ([Client], [Operation], [AlertThresholdMinutes]);
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[ClientInactivityAlertSettings]', N'U') IS NOT NULL
                   AND EXISTS (
                       SELECT 1
                       FROM sys.indexes
                       WHERE name = N'IX_ClientInactivityAlertSettings_Client_Operation_AlertThresholdMinutes'
                         AND object_id = OBJECT_ID(N'[ClientInactivityAlertSettings]')
                   )
                BEGIN
                    DROP INDEX [IX_ClientInactivityAlertSettings_Client_Operation_AlertThresholdMinutes]
                    ON [ClientInactivityAlertSettings];
                END;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[ClientInactivityAlertSettings]', N'U') IS NOT NULL
                   AND EXISTS (
                       SELECT 1
                       FROM sys.indexes
                       WHERE name = N'IX_ClientInactivityAlertSettings_Operation'
                         AND object_id = OBJECT_ID(N'[ClientInactivityAlertSettings]')
                   )
                BEGIN
                    DROP INDEX [IX_ClientInactivityAlertSettings_Operation]
                    ON [ClientInactivityAlertSettings];
                END;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[DF_ClientInactivityAlertSettings_Operation]', N'D') IS NOT NULL
                    ALTER TABLE [ClientInactivityAlertSettings]
                    DROP CONSTRAINT [DF_ClientInactivityAlertSettings_Operation];
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[ClientInactivityAlertSettings]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'ClientInactivityAlertSettings', N'Operation') IS NOT NULL
                BEGIN
                    ALTER TABLE [ClientInactivityAlertSettings]
                    DROP COLUMN [Operation];
                END;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[ClientInactivityAlertSettings]', N'U') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1
                       FROM sys.indexes
                       WHERE name = N'IX_ClientInactivityAlertSettings_Client_AlertThresholdMinutes'
                         AND object_id = OBJECT_ID(N'[ClientInactivityAlertSettings]')
                   )
                BEGIN
                    CREATE UNIQUE INDEX [IX_ClientInactivityAlertSettings_Client_AlertThresholdMinutes]
                    ON [ClientInactivityAlertSettings] ([Client], [AlertThresholdMinutes]);
                END;
                """);
        }
    }
}
