using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulseCheck.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeIdentityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[Devices]', N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH('Devices', 'EmployeeId') IS NULL
                        ALTER TABLE [Devices] ADD [EmployeeId] nvarchar(80) NOT NULL CONSTRAINT [DF_Devices_EmployeeId] DEFAULT N'';

                    IF COL_LENGTH('Devices', 'EntraObjectId') IS NULL
                        ALTER TABLE [Devices] ADD [EntraObjectId] nvarchar(80) NOT NULL CONSTRAINT [DF_Devices_EntraObjectId] DEFAULT N'';

                    IF COL_LENGTH('Devices', 'UserPrincipalName') IS NULL
                        ALTER TABLE [Devices] ADD [UserPrincipalName] nvarchar(180) NOT NULL CONSTRAINT [DF_Devices_UserPrincipalName] DEFAULT N'';

                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Devices_EmployeeId' AND object_id = OBJECT_ID(N'[Devices]'))
                        CREATE INDEX [IX_Devices_EmployeeId] ON [Devices] ([EmployeeId]);

                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Devices_EntraObjectId' AND object_id = OBJECT_ID(N'[Devices]'))
                        CREATE INDEX [IX_Devices_EntraObjectId] ON [Devices] ([EntraObjectId]);
                END;

                IF OBJECT_ID(N'[Responses]', N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH('Responses', 'EmployeeId') IS NULL
                        ALTER TABLE [Responses] ADD [EmployeeId] nvarchar(80) NOT NULL CONSTRAINT [DF_Responses_EmployeeId] DEFAULT N'';

                    IF COL_LENGTH('Responses', 'EntraObjectId') IS NULL
                        ALTER TABLE [Responses] ADD [EntraObjectId] nvarchar(80) NOT NULL CONSTRAINT [DF_Responses_EntraObjectId] DEFAULT N'';

                    IF COL_LENGTH('Responses', 'UserPrincipalName') IS NULL
                        ALTER TABLE [Responses] ADD [UserPrincipalName] nvarchar(180) NOT NULL CONSTRAINT [DF_Responses_UserPrincipalName] DEFAULT N'';
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
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Devices_EmployeeId' AND object_id = OBJECT_ID(N'[Devices]'))
                        DROP INDEX [IX_Devices_EmployeeId] ON [Devices];

                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Devices_EntraObjectId' AND object_id = OBJECT_ID(N'[Devices]'))
                        DROP INDEX [IX_Devices_EntraObjectId] ON [Devices];

                    IF OBJECT_ID(N'[DF_Devices_EmployeeId]', N'D') IS NOT NULL
                        ALTER TABLE [Devices] DROP CONSTRAINT [DF_Devices_EmployeeId];

                    IF COL_LENGTH('Devices', 'EmployeeId') IS NOT NULL
                        ALTER TABLE [Devices] DROP COLUMN [EmployeeId];

                    IF OBJECT_ID(N'[DF_Devices_EntraObjectId]', N'D') IS NOT NULL
                        ALTER TABLE [Devices] DROP CONSTRAINT [DF_Devices_EntraObjectId];

                    IF COL_LENGTH('Devices', 'EntraObjectId') IS NOT NULL
                        ALTER TABLE [Devices] DROP COLUMN [EntraObjectId];

                    IF OBJECT_ID(N'[DF_Devices_UserPrincipalName]', N'D') IS NOT NULL
                        ALTER TABLE [Devices] DROP CONSTRAINT [DF_Devices_UserPrincipalName];

                    IF COL_LENGTH('Devices', 'UserPrincipalName') IS NOT NULL
                        ALTER TABLE [Devices] DROP COLUMN [UserPrincipalName];
                END;

                IF OBJECT_ID(N'[Responses]', N'U') IS NOT NULL
                BEGIN
                    IF OBJECT_ID(N'[DF_Responses_EmployeeId]', N'D') IS NOT NULL
                        ALTER TABLE [Responses] DROP CONSTRAINT [DF_Responses_EmployeeId];

                    IF COL_LENGTH('Responses', 'EmployeeId') IS NOT NULL
                        ALTER TABLE [Responses] DROP COLUMN [EmployeeId];

                    IF OBJECT_ID(N'[DF_Responses_EntraObjectId]', N'D') IS NOT NULL
                        ALTER TABLE [Responses] DROP CONSTRAINT [DF_Responses_EntraObjectId];

                    IF COL_LENGTH('Responses', 'EntraObjectId') IS NOT NULL
                        ALTER TABLE [Responses] DROP COLUMN [EntraObjectId];

                    IF OBJECT_ID(N'[DF_Responses_UserPrincipalName]', N'D') IS NOT NULL
                        ALTER TABLE [Responses] DROP CONSTRAINT [DF_Responses_UserPrincipalName];

                    IF COL_LENGTH('Responses', 'UserPrincipalName') IS NOT NULL
                        ALTER TABLE [Responses] DROP COLUMN [UserPrincipalName];
                END;
                """);
        }
    }
}
