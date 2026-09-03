using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulseCheck.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminRolesAndAgentCredentialExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[AgentCredentials]', N'U') IS NOT NULL
                   AND COL_LENGTH('AgentCredentials', 'ExpiresAtUtc') IS NULL
                BEGIN
                    ALTER TABLE [AgentCredentials]
                    ADD [ExpiresAtUtc] datetimeoffset NOT NULL
                    CONSTRAINT [DF_AgentCredentials_ExpiresAtUtc] DEFAULT DATEADD(day, 180, SYSUTCDATETIME());
                END;

                IF OBJECT_ID(N'[AdminUsers]', N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH('AdminUsers', 'Role') IS NULL
                    BEGIN
                        ALTER TABLE [AdminUsers]
                        ADD [Role] nvarchar(32) NOT NULL
                        CONSTRAINT [DF_AdminUsers_Role] DEFAULT N'Admin';
                    END;

                    UPDATE [AdminUsers]
                    SET [Role] = N'Owner'
                    WHERE LOWER([Email]) = N'carlos.colon@solvoglobal.com';
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[AgentCredentials]', N'U') IS NOT NULL
                   AND COL_LENGTH('AgentCredentials', 'ExpiresAtUtc') IS NOT NULL
                BEGIN
                    IF OBJECT_ID(N'[DF_AgentCredentials_ExpiresAtUtc]', N'D') IS NOT NULL
                    BEGIN
                        ALTER TABLE [AgentCredentials] DROP CONSTRAINT [DF_AgentCredentials_ExpiresAtUtc];
                    END;

                    ALTER TABLE [AgentCredentials] DROP COLUMN [ExpiresAtUtc];
                END;

                IF OBJECT_ID(N'[AdminUsers]', N'U') IS NOT NULL
                   AND COL_LENGTH('AdminUsers', 'Role') IS NOT NULL
                BEGIN
                    IF OBJECT_ID(N'[DF_AdminUsers_Role]', N'D') IS NOT NULL
                    BEGIN
                        ALTER TABLE [AdminUsers] DROP CONSTRAINT [DF_AdminUsers_Role];
                    END;

                    ALTER TABLE [AdminUsers] DROP COLUMN [Role];
                END;
                """);
        }
    }
}
