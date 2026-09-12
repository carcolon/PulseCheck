using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulseCheck.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientInactivityAlertSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[Devices]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'Devices', N'Client') IS NULL
                BEGIN
                    ALTER TABLE [Devices] ADD [Client] nvarchar(180) NOT NULL CONSTRAINT [DF_Devices_Client] DEFAULT N'';
                END;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[AgentActivityEvents]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'AgentActivityEvents', N'OccurredAtLocal') IS NULL
                BEGIN
                    ALTER TABLE [AgentActivityEvents] ADD [OccurredAtLocal] datetimeoffset NULL;
                END;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[ClientInactivityAlertSettings]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [ClientInactivityAlertSettings] (
                        [Id] uniqueidentifier NOT NULL,
                        [Client] nvarchar(180) NOT NULL,
                        [AlertThresholdMinutes] int NOT NULL,
                        [IsEnabled] bit NOT NULL,
                        [CreatedAtUtc] datetimeoffset NOT NULL,
                        [UpdatedAtUtc] datetimeoffset NOT NULL,
                        CONSTRAINT [PK_ClientInactivityAlertSettings] PRIMARY KEY ([Id])
                    );
                END;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[LockedSessionAlertNotifications]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [LockedSessionAlertNotifications] (
                        [Id] uniqueidentifier NOT NULL,
                        [DeviceId] nvarchar(80) NOT NULL,
                        [EmployeeId] nvarchar(80) NOT NULL,
                        [Client] nvarchar(180) NOT NULL,
                        [LockedAtUtc] datetimeoffset NOT NULL,
                        [ThresholdMinutes] int NOT NULL,
                        [SentAtUtc] datetimeoffset NOT NULL,
                        CONSTRAINT [PK_LockedSessionAlertNotifications] PRIMARY KEY ([Id])
                    );
                END;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[Devices]', N'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Devices_Client' AND object_id = OBJECT_ID(N'[Devices]'))
                BEGIN
                    CREATE INDEX [IX_Devices_Client] ON [Devices] ([Client]);
                END;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[ClientInactivityAlertSettings]', N'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ClientInactivityAlertSettings_Client' AND object_id = OBJECT_ID(N'[ClientInactivityAlertSettings]'))
                BEGIN
                    CREATE INDEX [IX_ClientInactivityAlertSettings_Client] ON [ClientInactivityAlertSettings] ([Client]);
                END;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[ClientInactivityAlertSettings]', N'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ClientInactivityAlertSettings_Client_AlertThresholdMinutes' AND object_id = OBJECT_ID(N'[ClientInactivityAlertSettings]'))
                BEGIN
                    CREATE UNIQUE INDEX [IX_ClientInactivityAlertSettings_Client_AlertThresholdMinutes] ON [ClientInactivityAlertSettings] ([Client], [AlertThresholdMinutes]);
                END;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[LockedSessionAlertNotifications]', N'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LockedSessionAlertNotifications_DeviceId_LockedAtUtc_ThresholdMinutes' AND object_id = OBJECT_ID(N'[LockedSessionAlertNotifications]'))
                BEGIN
                    CREATE UNIQUE INDEX [IX_LockedSessionAlertNotifications_DeviceId_LockedAtUtc_ThresholdMinutes] ON [LockedSessionAlertNotifications] ([DeviceId], [LockedAtUtc], [ThresholdMinutes]);
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[LockedSessionAlertNotifications]', N'U') IS NOT NULL
                    DROP TABLE [LockedSessionAlertNotifications];
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[ClientInactivityAlertSettings]', N'U') IS NOT NULL
                    DROP TABLE [ClientInactivityAlertSettings];
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[Devices]', N'U') IS NOT NULL
                   AND EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Devices_Client' AND object_id = OBJECT_ID(N'[Devices]'))
                BEGIN
                    DROP INDEX [IX_Devices_Client] ON [Devices];
                END;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[DF_Devices_Client]', N'D') IS NOT NULL
                    ALTER TABLE [Devices] DROP CONSTRAINT [DF_Devices_Client];
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[Devices]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'Devices', N'Client') IS NOT NULL
                BEGIN
                    ALTER TABLE [Devices] DROP COLUMN [Client];
                END;
                """);
        }
    }
}
