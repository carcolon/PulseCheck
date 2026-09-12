using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulseCheck.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFabricOperationsProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[Devices]', N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH('Devices', 'Operation') IS NULL
                        ALTER TABLE [Devices] ADD [Operation] nvarchar(180) NOT NULL CONSTRAINT [DF_Devices_Operation] DEFAULT N'';

                    IF COL_LENGTH('Devices', 'EmployeeStatus') IS NULL
                        ALTER TABLE [Devices] ADD [EmployeeStatus] nvarchar(80) NOT NULL CONSTRAINT [DF_Devices_EmployeeStatus] DEFAULT N'';

                    IF COL_LENGTH('Devices', 'LeaderCompanyId') IS NULL
                        ALTER TABLE [Devices] ADD [LeaderCompanyId] nvarchar(80) NOT NULL CONSTRAINT [DF_Devices_LeaderCompanyId] DEFAULT N'';

                    IF COL_LENGTH('Devices', 'LeaderFullName') IS NULL
                        ALTER TABLE [Devices] ADD [LeaderFullName] nvarchar(180) NOT NULL CONSTRAINT [DF_Devices_LeaderFullName] DEFAULT N'';

                    IF COL_LENGTH('Devices', 'LeaderCorporateEmail') IS NULL
                        ALTER TABLE [Devices] ADD [LeaderCorporateEmail] nvarchar(180) NOT NULL CONSTRAINT [DF_Devices_LeaderCorporateEmail] DEFAULT N'';

                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Devices_Operation' AND object_id = OBJECT_ID(N'[Devices]'))
                        CREATE INDEX [IX_Devices_Operation] ON [Devices] ([Operation]);

                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Devices_LeaderCompanyId' AND object_id = OBJECT_ID(N'[Devices]'))
                        CREATE INDEX [IX_Devices_LeaderCompanyId] ON [Devices] ([LeaderCompanyId]);
                END;

                IF OBJECT_ID(N'[Responses]', N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH('Responses', 'Operation') IS NULL
                        ALTER TABLE [Responses] ADD [Operation] nvarchar(180) NOT NULL CONSTRAINT [DF_Responses_Operation] DEFAULT N'';

                    IF COL_LENGTH('Responses', 'EmployeeStatus') IS NULL
                        ALTER TABLE [Responses] ADD [EmployeeStatus] nvarchar(80) NOT NULL CONSTRAINT [DF_Responses_EmployeeStatus] DEFAULT N'';

                    IF COL_LENGTH('Responses', 'LeaderCompanyId') IS NULL
                        ALTER TABLE [Responses] ADD [LeaderCompanyId] nvarchar(80) NOT NULL CONSTRAINT [DF_Responses_LeaderCompanyId] DEFAULT N'';

                    IF COL_LENGTH('Responses', 'LeaderFullName') IS NULL
                        ALTER TABLE [Responses] ADD [LeaderFullName] nvarchar(180) NOT NULL CONSTRAINT [DF_Responses_LeaderFullName] DEFAULT N'';

                    IF COL_LENGTH('Responses', 'LeaderCorporateEmail') IS NULL
                        ALTER TABLE [Responses] ADD [LeaderCorporateEmail] nvarchar(180) NOT NULL CONSTRAINT [DF_Responses_LeaderCorporateEmail] DEFAULT N'';
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[Devices]', N'U') IS NOT NULL
                BEGIN
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Devices_Operation' AND object_id = OBJECT_ID(N'[Devices]'))
                        DROP INDEX [IX_Devices_Operation] ON [Devices];

                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Devices_LeaderCompanyId' AND object_id = OBJECT_ID(N'[Devices]'))
                        DROP INDEX [IX_Devices_LeaderCompanyId] ON [Devices];

                    IF OBJECT_ID(N'[DF_Devices_Operation]', N'D') IS NOT NULL
                        ALTER TABLE [Devices] DROP CONSTRAINT [DF_Devices_Operation];

                    IF COL_LENGTH('Devices', 'Operation') IS NOT NULL
                        ALTER TABLE [Devices] DROP COLUMN [Operation];

                    IF OBJECT_ID(N'[DF_Devices_EmployeeStatus]', N'D') IS NOT NULL
                        ALTER TABLE [Devices] DROP CONSTRAINT [DF_Devices_EmployeeStatus];

                    IF COL_LENGTH('Devices', 'EmployeeStatus') IS NOT NULL
                        ALTER TABLE [Devices] DROP COLUMN [EmployeeStatus];

                    IF OBJECT_ID(N'[DF_Devices_LeaderCompanyId]', N'D') IS NOT NULL
                        ALTER TABLE [Devices] DROP CONSTRAINT [DF_Devices_LeaderCompanyId];

                    IF COL_LENGTH('Devices', 'LeaderCompanyId') IS NOT NULL
                        ALTER TABLE [Devices] DROP COLUMN [LeaderCompanyId];

                    IF OBJECT_ID(N'[DF_Devices_LeaderFullName]', N'D') IS NOT NULL
                        ALTER TABLE [Devices] DROP CONSTRAINT [DF_Devices_LeaderFullName];

                    IF COL_LENGTH('Devices', 'LeaderFullName') IS NOT NULL
                        ALTER TABLE [Devices] DROP COLUMN [LeaderFullName];

                    IF OBJECT_ID(N'[DF_Devices_LeaderCorporateEmail]', N'D') IS NOT NULL
                        ALTER TABLE [Devices] DROP CONSTRAINT [DF_Devices_LeaderCorporateEmail];

                    IF COL_LENGTH('Devices', 'LeaderCorporateEmail') IS NOT NULL
                        ALTER TABLE [Devices] DROP COLUMN [LeaderCorporateEmail];
                END;

                IF OBJECT_ID(N'[Responses]', N'U') IS NOT NULL
                BEGIN
                    IF OBJECT_ID(N'[DF_Responses_Operation]', N'D') IS NOT NULL
                        ALTER TABLE [Responses] DROP CONSTRAINT [DF_Responses_Operation];

                    IF COL_LENGTH('Responses', 'Operation') IS NOT NULL
                        ALTER TABLE [Responses] DROP COLUMN [Operation];

                    IF OBJECT_ID(N'[DF_Responses_EmployeeStatus]', N'D') IS NOT NULL
                        ALTER TABLE [Responses] DROP CONSTRAINT [DF_Responses_EmployeeStatus];

                    IF COL_LENGTH('Responses', 'EmployeeStatus') IS NOT NULL
                        ALTER TABLE [Responses] DROP COLUMN [EmployeeStatus];

                    IF OBJECT_ID(N'[DF_Responses_LeaderCompanyId]', N'D') IS NOT NULL
                        ALTER TABLE [Responses] DROP CONSTRAINT [DF_Responses_LeaderCompanyId];

                    IF COL_LENGTH('Responses', 'LeaderCompanyId') IS NOT NULL
                        ALTER TABLE [Responses] DROP COLUMN [LeaderCompanyId];

                    IF OBJECT_ID(N'[DF_Responses_LeaderFullName]', N'D') IS NOT NULL
                        ALTER TABLE [Responses] DROP CONSTRAINT [DF_Responses_LeaderFullName];

                    IF COL_LENGTH('Responses', 'LeaderFullName') IS NOT NULL
                        ALTER TABLE [Responses] DROP COLUMN [LeaderFullName];

                    IF OBJECT_ID(N'[DF_Responses_LeaderCorporateEmail]', N'D') IS NOT NULL
                        ALTER TABLE [Responses] DROP CONSTRAINT [DF_Responses_LeaderCorporateEmail];

                    IF COL_LENGTH('Responses', 'LeaderCorporateEmail') IS NOT NULL
                        ALTER TABLE [Responses] DROP COLUMN [LeaderCorporateEmail];
                END;
                """);
        }
    }
}
