using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PulseCheck.Application.Security;
using PulseCheck.Domain.Entities;

namespace PulseCheck.Infrastructure.Persistence;

public sealed class PulseCheckDbInitializer(PulseCheckDbContext dbContext, IConfiguration configuration)
{
    private const string EmptyGuidText = "00000000-0000-0000-0000-000000000000";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureCompatibilityAsync(cancellationToken);
    }

    private async Task EnsureCompatibilityAsync(CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsSqlServer())
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID(N'[AdminUsers]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [AdminUsers] (
                        [Id] uniqueidentifier NOT NULL,
                        [Email] nvarchar(180) NOT NULL,
                        [DisplayName] nvarchar(160) NOT NULL,
                        [EntraObjectId] nvarchar(80) NULL,
                        [TenantId] nvarchar(80) NULL,
                        [AuthenticationMode] nvarchar(32) NOT NULL,
                        [Role] nvarchar(32) NOT NULL CONSTRAINT [DF_AdminUsers_Role] DEFAULT N'Admin',
                        [PasswordHash] nvarchar(500) NOT NULL,
                        [IsActive] bit NOT NULL,
                        [CreatedAtUtc] datetimeoffset NOT NULL,
                        [LastLoginAtUtc] datetimeoffset NULL,
                        CONSTRAINT [PK_AdminUsers] PRIMARY KEY ([Id])
                    );

                    CREATE UNIQUE INDEX [IX_AdminUsers_Email] ON [AdminUsers] ([Email]);
                END;
                """,
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('AdminUsers', 'AuthenticationMode') IS NULL
                BEGIN
                    ALTER TABLE [AdminUsers]
                    ADD [AuthenticationMode] nvarchar(32) NOT NULL
                    CONSTRAINT [DF_AdminUsers_AuthenticationMode] DEFAULT N'Local';
                END;

                IF COL_LENGTH('AdminUsers', 'EntraObjectId') IS NULL
                BEGIN
                    ALTER TABLE [AdminUsers]
                    ADD [EntraObjectId] nvarchar(80) NULL;
                END;

                IF COL_LENGTH('AdminUsers', 'TenantId') IS NULL
                BEGIN
                    ALTER TABLE [AdminUsers]
                    ADD [TenantId] nvarchar(80) NULL;
                END;

                IF COL_LENGTH('AdminUsers', 'Role') IS NULL
                BEGIN
                    ALTER TABLE [AdminUsers]
                    ADD [Role] nvarchar(32) NOT NULL
                    CONSTRAINT [DF_AdminUsers_Role] DEFAULT N'Admin';
                END;

                IF COL_LENGTH('AdminUsers', 'AuthenticationMode') IS NOT NULL
                BEGIN
                    EXEC(N'
                        UPDATE [AdminUsers]
                        SET [AuthenticationMode] = N''Local''
                        WHERE [AuthenticationMode] IS NULL OR LTRIM(RTRIM([AuthenticationMode])) = N'''';
                    ');
                END;

                IF COL_LENGTH('AdminUsers', 'Role') IS NOT NULL
                BEGIN
                    EXEC(N'
                        UPDATE [AdminUsers]
                        SET [Role] = N''Admin''
                        WHERE [Role] IS NULL OR LTRIM(RTRIM([Role])) = N'''';

                        UPDATE [AdminUsers]
                        SET [Role] = N''Owner''
                        WHERE LOWER([Email]) = N''carlos.colon@solvoglobal.com'';
                    ');
                END;
                """,
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID(N'[AgentCredentials]', N'U') IS NOT NULL
                   AND COL_LENGTH('AgentCredentials', 'ExpiresAtUtc') IS NULL
                BEGIN
                    ALTER TABLE [AgentCredentials]
                    ADD [ExpiresAtUtc] datetimeoffset NOT NULL
                    CONSTRAINT [DF_AgentCredentials_ExpiresAtUtc] DEFAULT DATEADD(day, 180, SYSUTCDATETIME());
                END;
                """,
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID(N'[AdminSessions]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [AdminSessions] (
                        [Id] uniqueidentifier NOT NULL,
                        [AdminUserId] uniqueidentifier NOT NULL,
                        [TokenHash] nvarchar(64) NOT NULL,
                        [UserAgent] nvarchar(500) NULL,
                        [IpAddress] nvarchar(120) NULL,
                        [CreatedAtUtc] datetimeoffset NOT NULL,
                        [ExpiresAtUtc] datetimeoffset NOT NULL,
                        [RevokedAtUtc] datetimeoffset NULL,
                        CONSTRAINT [PK_AdminSessions] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_AdminSessions_AdminUsers_AdminUserId] FOREIGN KEY ([AdminUserId]) REFERENCES [AdminUsers] ([Id]) ON DELETE CASCADE
                    );

                    CREATE UNIQUE INDEX [IX_AdminSessions_TokenHash] ON [AdminSessions] ([TokenHash]);
                END;
                """,
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID(N'[AgentActivityEvents]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [AgentActivityEvents] (
                        [Id] uniqueidentifier NOT NULL,
                        [DeviceId] nvarchar(80) NOT NULL,
                        [UserId] nvarchar(120) NOT NULL,
                        [UserName] nvarchar(160) NOT NULL,
                        [Email] nvarchar(160) NOT NULL,
                        [Department] nvarchar(120) NOT NULL,
                        [Hostname] nvarchar(120) NOT NULL,
                        [EventType] nvarchar(48) NOT NULL,
                        [LockReason] nvarchar(48) NULL,
                        [IdleSecondsAtLock] int NULL,
                        [DurationSeconds] int NULL,
                        [OccurredAtUtc] datetimeoffset NOT NULL,
                        [OccurredAtLocal] datetimeoffset NULL,
                        CONSTRAINT [PK_AgentActivityEvents] PRIMARY KEY ([Id])
                    );
                END;
                """,
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('AgentActivityEvents', 'OccurredAtLocal') IS NULL
                BEGIN
                    ALTER TABLE [AgentActivityEvents] ADD [OccurredAtLocal] datetimeoffset NULL;
                END
                """,
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('Devices', 'EmployeeId') IS NULL
                BEGIN
                    ALTER TABLE [Devices] ADD [EmployeeId] nvarchar(80) NOT NULL CONSTRAINT [DF_Devices_EmployeeId] DEFAULT N'';
                END;

                IF COL_LENGTH('Devices', 'EntraObjectId') IS NULL
                BEGIN
                    ALTER TABLE [Devices] ADD [EntraObjectId] nvarchar(80) NOT NULL CONSTRAINT [DF_Devices_EntraObjectId] DEFAULT N'';
                END;

                IF COL_LENGTH('Devices', 'UserPrincipalName') IS NULL
                BEGIN
                    ALTER TABLE [Devices] ADD [UserPrincipalName] nvarchar(180) NOT NULL CONSTRAINT [DF_Devices_UserPrincipalName] DEFAULT N'';
                END;

                IF COL_LENGTH('Devices', 'Operation') IS NULL
                BEGIN
                    ALTER TABLE [Devices] ADD [Operation] nvarchar(180) NOT NULL CONSTRAINT [DF_Devices_Operation] DEFAULT N'';
                END;

                IF COL_LENGTH('Devices', 'EmployeeStatus') IS NULL
                BEGIN
                    ALTER TABLE [Devices] ADD [EmployeeStatus] nvarchar(80) NOT NULL CONSTRAINT [DF_Devices_EmployeeStatus] DEFAULT N'';
                END;

                IF COL_LENGTH('Devices', 'LeaderSolvoId') IS NULL
                BEGIN
                    ALTER TABLE [Devices] ADD [LeaderSolvoId] nvarchar(80) NOT NULL CONSTRAINT [DF_Devices_LeaderSolvoId] DEFAULT N'';
                END;

                IF COL_LENGTH('Devices', 'LeaderFullName') IS NULL
                BEGIN
                    ALTER TABLE [Devices] ADD [LeaderFullName] nvarchar(180) NOT NULL CONSTRAINT [DF_Devices_LeaderFullName] DEFAULT N'';
                END;

                IF COL_LENGTH('Devices', 'LeaderCorporateEmail') IS NULL
                BEGIN
                    ALTER TABLE [Devices] ADD [LeaderCorporateEmail] nvarchar(180) NOT NULL CONSTRAINT [DF_Devices_LeaderCorporateEmail] DEFAULT N'';
                END;

                IF COL_LENGTH('Responses', 'EmployeeId') IS NULL
                BEGIN
                    ALTER TABLE [Responses] ADD [EmployeeId] nvarchar(80) NOT NULL CONSTRAINT [DF_Responses_EmployeeId] DEFAULT N'';
                END;

                IF COL_LENGTH('Responses', 'EntraObjectId') IS NULL
                BEGIN
                    ALTER TABLE [Responses] ADD [EntraObjectId] nvarchar(80) NOT NULL CONSTRAINT [DF_Responses_EntraObjectId] DEFAULT N'';
                END;

                IF COL_LENGTH('Responses', 'UserPrincipalName') IS NULL
                BEGIN
                    ALTER TABLE [Responses] ADD [UserPrincipalName] nvarchar(180) NOT NULL CONSTRAINT [DF_Responses_UserPrincipalName] DEFAULT N'';
                END;

                IF COL_LENGTH('Responses', 'Operation') IS NULL
                BEGIN
                    ALTER TABLE [Responses] ADD [Operation] nvarchar(180) NOT NULL CONSTRAINT [DF_Responses_Operation] DEFAULT N'';
                END;

                IF COL_LENGTH('Responses', 'EmployeeStatus') IS NULL
                BEGIN
                    ALTER TABLE [Responses] ADD [EmployeeStatus] nvarchar(80) NOT NULL CONSTRAINT [DF_Responses_EmployeeStatus] DEFAULT N'';
                END;

                IF COL_LENGTH('Responses', 'LeaderSolvoId') IS NULL
                BEGIN
                    ALTER TABLE [Responses] ADD [LeaderSolvoId] nvarchar(80) NOT NULL CONSTRAINT [DF_Responses_LeaderSolvoId] DEFAULT N'';
                END;

                IF COL_LENGTH('Responses', 'LeaderFullName') IS NULL
                BEGIN
                    ALTER TABLE [Responses] ADD [LeaderFullName] nvarchar(180) NOT NULL CONSTRAINT [DF_Responses_LeaderFullName] DEFAULT N'';
                END;

                IF COL_LENGTH('Responses', 'LeaderCorporateEmail') IS NULL
                BEGIN
                    ALTER TABLE [Responses] ADD [LeaderCorporateEmail] nvarchar(180) NOT NULL CONSTRAINT [DF_Responses_LeaderCorporateEmail] DEFAULT N'';
                END;

                IF COL_LENGTH('Campaigns', 'QuestionsJson') IS NULL
                BEGIN
                    ALTER TABLE [Campaigns]
                    ADD [QuestionsJson] nvarchar(max) NOT NULL
                    CONSTRAINT [DF_Campaigns_QuestionsJson] DEFAULT N'[]';
                END;
                """,
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('Responses', 'QuestionId') IS NULL
                BEGIN
                    ALTER TABLE [Responses]
                    ADD [QuestionId] uniqueidentifier NOT NULL
                    CONSTRAINT [DF_Responses_QuestionId] DEFAULT '00000000-0000-0000-0000-000000000000';
                END;
                """,
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('Responses', 'QuestionText') IS NULL
                BEGIN
                    ALTER TABLE [Responses]
                    ADD [QuestionText] nvarchar(500) NOT NULL
                    CONSTRAINT [DF_Responses_QuestionText] DEFAULT N'';
                END;
                """,
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('Responses', 'QuestionType') IS NULL
                BEGIN
                    ALTER TABLE [Responses]
                    ADD [QuestionType] int NOT NULL
                    CONSTRAINT [DF_Responses_QuestionType] DEFAULT 0;
                END;
                """,
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('Responses', 'Value') IS NULL
                BEGIN
                    ALTER TABLE [Responses]
                    ADD [Value] int NOT NULL
                    CONSTRAINT [DF_Responses_Value] DEFAULT 0;
                END;
                """,
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('Responses', 'NumericValue') IS NULL
                BEGIN
                    ALTER TABLE [Responses] ADD [NumericValue] int NULL;
                END;
                """,
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('Responses', 'TextValue') IS NULL
                BEGIN
                    ALTER TABLE [Responses] ADD [TextValue] nvarchar(2000) NULL;
                END;
                """,
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('Responses', 'SubmissionId') IS NULL
                BEGIN
                    ALTER TABLE [Responses]
                    ADD [SubmissionId] uniqueidentifier NOT NULL
                    CONSTRAINT [DF_Responses_SubmissionId] DEFAULT '00000000-0000-0000-0000-000000000000';
                END;
                """,
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('Responses', 'QuestionText') IS NOT NULL
                BEGIN
                    UPDATE r
                    SET r.[QuestionText] = COALESCE(NULLIF(r.[QuestionText], N''), c.[QuestionText], N'Pregunta')
                    FROM [Responses] r
                    LEFT JOIN [Campaigns] c ON c.[Id] = r.[CampaignId]
                    WHERE r.[QuestionText] IS NULL OR LTRIM(RTRIM(r.[QuestionText])) = N'';
                END;
                """,
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('Responses', 'QuestionId') IS NOT NULL
                BEGIN
                    UPDATE [Responses]
                    SET [QuestionId] = [CampaignId]
                    WHERE [QuestionId] = '00000000-0000-0000-0000-000000000000';
                END;
                """,
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('Responses', 'SubmissionId') IS NOT NULL
                BEGIN
                    UPDATE [Responses]
                    SET [SubmissionId] = [Id]
                    WHERE [SubmissionId] = '00000000-0000-0000-0000-000000000000';
                END;
                """,
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('Campaigns', 'QuestionsJson') IS NOT NULL
                BEGIN
                    UPDATE [Campaigns]
                    SET [QuestionsJson] = N'[]'
                    WHERE [QuestionsJson] IS NULL OR LTRIM(RTRIM([QuestionsJson])) = N'';
                END;
                """,
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID(N'[TransformationalLeaderAssignments]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [TransformationalLeaderAssignments] (
                        [Id] uniqueidentifier NOT NULL,
                        [SolvoId] nvarchar(80) NOT NULL,
                        [Operation] nvarchar(180) NOT NULL,
                        [OperationsJson] nvarchar(2000) NOT NULL CONSTRAINT [DF_TransformationalLeaderAssignments_OperationsJson] DEFAULT N'[]',
                        [CreatedAtUtc] datetimeoffset NOT NULL,
                        [UpdatedAtUtc] datetimeoffset NOT NULL,
                        CONSTRAINT [PK_TransformationalLeaderAssignments] PRIMARY KEY ([Id])
                    );

                    CREATE UNIQUE INDEX [IX_TransformationalLeaderAssignments_SolvoId] ON [TransformationalLeaderAssignments] ([SolvoId]);
                    CREATE INDEX [IX_TransformationalLeaderAssignments_Operation] ON [TransformationalLeaderAssignments] ([Operation]);
                END;

                IF COL_LENGTH('TransformationalLeaderAssignments', 'OperationsJson') IS NULL
                    ALTER TABLE [TransformationalLeaderAssignments] ADD [OperationsJson] nvarchar(2000) NOT NULL CONSTRAINT [DF_TransformationalLeaderAssignments_OperationsJson] DEFAULT N'[]';

                UPDATE [TransformationalLeaderAssignments]
                SET [OperationsJson] = CASE
                    WHEN LTRIM(RTRIM([Operation])) = N'' THEN N'[]'
                    ELSE CONCAT(N'["', STRING_ESCAPE([Operation], 'json'), N'"]')
                END
                WHERE [OperationsJson] IS NULL OR [OperationsJson] = N'[]';
                """,
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID(N'[TransformationalLeaderSessions]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [TransformationalLeaderSessions] (
                        [Id] uniqueidentifier NOT NULL,
                        [SolvoId] nvarchar(80) NOT NULL,
                        [Email] nvarchar(180) NOT NULL,
                        [DisplayName] nvarchar(180) NOT NULL,
                        [Operation] nvarchar(180) NOT NULL,
                        [OperationsJson] nvarchar(2000) NOT NULL CONSTRAINT [DF_TransformationalLeaderSessions_OperationsJson] DEFAULT N'[]',
                        [TokenHash] nvarchar(64) NOT NULL,
                        [UserAgent] nvarchar(500) NULL,
                        [IpAddress] nvarchar(120) NULL,
                        [CreatedAtUtc] datetimeoffset NOT NULL,
                        [ExpiresAtUtc] datetimeoffset NOT NULL,
                        [RevokedAtUtc] datetimeoffset NULL,
                        CONSTRAINT [PK_TransformationalLeaderSessions] PRIMARY KEY ([Id])
                    );

                    CREATE UNIQUE INDEX [IX_TransformationalLeaderSessions_TokenHash] ON [TransformationalLeaderSessions] ([TokenHash]);
                    CREATE INDEX [IX_TransformationalLeaderSessions_SolvoId] ON [TransformationalLeaderSessions] ([SolvoId]);
                END;

                IF COL_LENGTH('TransformationalLeaderSessions', 'OperationsJson') IS NULL
                    ALTER TABLE [TransformationalLeaderSessions] ADD [OperationsJson] nvarchar(2000) NOT NULL CONSTRAINT [DF_TransformationalLeaderSessions_OperationsJson] DEFAULT N'[]';

                UPDATE [TransformationalLeaderSessions]
                SET [OperationsJson] = CASE
                    WHEN LTRIM(RTRIM([Operation])) = N'' THEN N'[]'
                    ELSE CONCAT(N'["', STRING_ESCAPE([Operation], 'json'), N'"]')
                END
                WHERE [OperationsJson] IS NULL OR [OperationsJson] = N'[]';
                """,
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID(N'[TransformationalLeaderCandidates]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [TransformationalLeaderCandidates] (
                        [Id] uniqueidentifier NOT NULL,
                        [SolvoId] nvarchar(80) NOT NULL,
                        [FullName] nvarchar(180) NOT NULL,
                        [CorporateEmail] nvarchar(180) NOT NULL,
                        [JobTitleCode] nvarchar(80) NOT NULL,
                        [Status] nvarchar(80) NOT NULL,
                        [Operation] nvarchar(180) NOT NULL,
                        [Client] nvarchar(180) NOT NULL,
                        [Department] nvarchar(180) NOT NULL,
                        [IsActive] bit NOT NULL,
                        [CreatedAtUtc] datetimeoffset NOT NULL,
                        [UpdatedAtUtc] datetimeoffset NOT NULL,
                        [LastSyncedAtUtc] datetimeoffset NOT NULL,
                        CONSTRAINT [PK_TransformationalLeaderCandidates] PRIMARY KEY ([Id])
                    );

                    CREATE UNIQUE INDEX [IX_TransformationalLeaderCandidates_SolvoId] ON [TransformationalLeaderCandidates] ([SolvoId]);
                    CREATE INDEX [IX_TransformationalLeaderCandidates_IsActive] ON [TransformationalLeaderCandidates] ([IsActive]);
                    CREATE INDEX [IX_TransformationalLeaderCandidates_Operation] ON [TransformationalLeaderCandidates] ([Operation]);
                    CREATE INDEX [IX_TransformationalLeaderCandidates_CorporateEmail] ON [TransformationalLeaderCandidates] ([CorporateEmail]);
                END;
                """,
                cancellationToken);

            await EnsureBootstrapAdminAsync(cancellationToken);
            return;
        }

        if (dbContext.Database.IsSqlite())
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "AdminUsers" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_AdminUsers" PRIMARY KEY,
                    "Email" TEXT NOT NULL,
                    "DisplayName" TEXT NOT NULL,
                    "EntraObjectId" TEXT NULL,
                    "TenantId" TEXT NULL,
                    "AuthenticationMode" TEXT NOT NULL DEFAULT 'Local',
                    "Role" TEXT NOT NULL DEFAULT 'Admin',
                    "PasswordHash" TEXT NOT NULL,
                    "IsActive" INTEGER NOT NULL,
                    "CreatedAtUtc" TEXT NOT NULL,
                    "LastLoginAtUtc" TEXT NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_AdminUsers_Email" ON "AdminUsers" ("Email");

                CREATE TABLE IF NOT EXISTS "AdminSessions" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_AdminSessions" PRIMARY KEY,
                    "AdminUserId" TEXT NOT NULL,
                    "TokenHash" TEXT NOT NULL,
                    "UserAgent" TEXT NULL,
                    "IpAddress" TEXT NULL,
                    "CreatedAtUtc" TEXT NOT NULL,
                    "ExpiresAtUtc" TEXT NOT NULL,
                    "RevokedAtUtc" TEXT NULL,
                    CONSTRAINT "FK_AdminSessions_AdminUsers_AdminUserId" FOREIGN KEY ("AdminUserId") REFERENCES "AdminUsers" ("Id") ON DELETE CASCADE
                );

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_AdminSessions_TokenHash" ON "AdminSessions" ("TokenHash");

                CREATE TABLE IF NOT EXISTS "AgentActivityEvents" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_AgentActivityEvents" PRIMARY KEY,
                    "DeviceId" TEXT NOT NULL,
                    "UserId" TEXT NOT NULL,
                    "UserName" TEXT NOT NULL,
                    "Email" TEXT NOT NULL,
                    "Department" TEXT NOT NULL,
                    "Hostname" TEXT NOT NULL,
                    "EventType" TEXT NOT NULL,
                    "LockReason" TEXT NULL,
                    "IdleSecondsAtLock" INTEGER NULL,
                    "DurationSeconds" INTEGER NULL,
                    "OccurredAtUtc" TEXT NOT NULL,
                    "OccurredAtLocal" TEXT NULL
                );
                """,
                cancellationToken);

            await EnsureSqliteColumnAsync("AdminUsers", "AuthenticationMode", "TEXT NOT NULL DEFAULT 'Local'", cancellationToken);
            await EnsureSqliteColumnAsync("AdminUsers", "EntraObjectId", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync("AdminUsers", "TenantId", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync("AdminUsers", "Role", "TEXT NOT NULL DEFAULT 'Admin'", cancellationToken);
            await EnsureSqliteColumnIfTableExistsAsync("AgentCredentials", "ExpiresAtUtc", "TEXT NOT NULL DEFAULT '9999-12-31T23:59:59+00:00'", cancellationToken);
            await EnsureSqliteColumnAsync("Devices", "EmployeeId", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureSqliteColumnAsync("Devices", "EntraObjectId", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureSqliteColumnAsync("Devices", "UserPrincipalName", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureSqliteColumnAsync("Devices", "Operation", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureSqliteColumnAsync("Devices", "EmployeeStatus", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureSqliteColumnAsync("Devices", "LeaderSolvoId", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureSqliteColumnAsync("Devices", "LeaderFullName", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureSqliteColumnAsync("Devices", "LeaderCorporateEmail", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureSqliteColumnAsync("Responses", "EmployeeId", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureSqliteColumnAsync("Responses", "EntraObjectId", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureSqliteColumnAsync("Responses", "UserPrincipalName", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureSqliteColumnAsync("Responses", "Operation", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureSqliteColumnAsync("Responses", "EmployeeStatus", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureSqliteColumnAsync("Responses", "LeaderSolvoId", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureSqliteColumnAsync("Responses", "LeaderFullName", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureSqliteColumnAsync("Responses", "LeaderCorporateEmail", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureSqliteColumnAsync("AgentActivityEvents", "OccurredAtLocal", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync("ClientInactivityAlertSettings", "AdditionalRecipientEmailsJson", "TEXT NOT NULL DEFAULT '[]'", cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                UPDATE "AdminUsers"
                SET "AuthenticationMode" = 'Local'
                WHERE "AuthenticationMode" IS NULL OR TRIM("AuthenticationMode") = '';

                UPDATE "AdminUsers"
                SET "Role" = 'Admin'
                WHERE "Role" IS NULL OR TRIM("Role") = '';

                UPDATE "AdminUsers"
                SET "Role" = 'Owner'
                WHERE LOWER("Email") = 'carlos.colon@solvoglobal.com';
                """,
                cancellationToken);

            await EnsureSqliteColumnAsync("Campaigns", "QuestionsJson", "TEXT NOT NULL DEFAULT '[]'", cancellationToken);
            await EnsureSqliteColumnAsync("Responses", "QuestionId", $"TEXT NOT NULL DEFAULT '{EmptyGuidText}'", cancellationToken);
            await EnsureSqliteColumnAsync("Responses", "QuestionText", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureSqliteColumnAsync("Responses", "QuestionType", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureSqliteColumnAsync("Responses", "Value", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureSqliteColumnAsync("Responses", "NumericValue", "INTEGER NULL", cancellationToken);
            await EnsureSqliteColumnAsync("Responses", "TextValue", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync("Responses", "SubmissionId", $"TEXT NOT NULL DEFAULT '{EmptyGuidText}'", cancellationToken);
            await EnsureSqliteColumnIfTableExistsAsync("TransformationalLeaderAssignments", "OperationsJson", "TEXT NOT NULL DEFAULT '[]'", cancellationToken);
            await EnsureSqliteColumnIfTableExistsAsync("TransformationalLeaderSessions", "OperationsJson", "TEXT NOT NULL DEFAULT '[]'", cancellationToken);
            await EnsureSqliteColumnIfTableExistsAsync("TransformationalLeaderExportJobs", "OperationsJson", "TEXT NOT NULL DEFAULT '[]'", cancellationToken);

            if (await SqliteColumnExistsAsync("Responses", "Value", cancellationToken))
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    """
                    UPDATE "Responses"
                    SET "NumericValue" = COALESCE("NumericValue", "Value")
                    WHERE "Value" IS NOT NULL;
                    """,
                    cancellationToken);
            }

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                UPDATE "Responses"
                SET "QuestionText" = COALESCE(NULLIF("QuestionText", ''), (
                    SELECT "QuestionText"
                    FROM "Campaigns" c
                    WHERE c."Id" = "Responses"."CampaignId"
                ), 'Pregunta')
                WHERE "QuestionText" IS NULL OR TRIM("QuestionText") = '';

                UPDATE "Responses"
                SET "QuestionId" = "CampaignId"
                WHERE "QuestionId" = '00000000-0000-0000-0000-000000000000';

                UPDATE "Responses"
                SET "SubmissionId" = "Id"
                WHERE "SubmissionId" = '00000000-0000-0000-0000-000000000000';

                UPDATE "Campaigns"
                SET "QuestionsJson" = '[]'
                WHERE "QuestionsJson" IS NULL OR TRIM("QuestionsJson") = '';
                """,
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "TransformationalLeaderAssignments" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_TransformationalLeaderAssignments" PRIMARY KEY,
                    "SolvoId" TEXT NOT NULL,
                    "Operation" TEXT NOT NULL,
                    "OperationsJson" TEXT NOT NULL DEFAULT '[]',
                    "CreatedAtUtc" TEXT NOT NULL,
                    "UpdatedAtUtc" TEXT NOT NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_TransformationalLeaderAssignments_SolvoId" ON "TransformationalLeaderAssignments" ("SolvoId");
                CREATE INDEX IF NOT EXISTS "IX_TransformationalLeaderAssignments_Operation" ON "TransformationalLeaderAssignments" ("Operation");

                UPDATE "TransformationalLeaderAssignments"
                SET "OperationsJson" = CASE
                    WHEN trim("Operation") = '' THEN '[]'
                    ELSE '["' || replace("Operation", '"', '\"') || '"]'
                END
                WHERE "OperationsJson" IS NULL OR "OperationsJson" = '[]';
                """,
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "TransformationalLeaderSessions" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_TransformationalLeaderSessions" PRIMARY KEY,
                    "SolvoId" TEXT NOT NULL,
                    "Email" TEXT NOT NULL,
                    "DisplayName" TEXT NOT NULL,
                    "Operation" TEXT NOT NULL,
                    "OperationsJson" TEXT NOT NULL DEFAULT '[]',
                    "TokenHash" TEXT NOT NULL,
                    "UserAgent" TEXT NULL,
                    "IpAddress" TEXT NULL,
                    "CreatedAtUtc" TEXT NOT NULL,
                    "ExpiresAtUtc" TEXT NOT NULL,
                    "RevokedAtUtc" TEXT NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_TransformationalLeaderSessions_TokenHash" ON "TransformationalLeaderSessions" ("TokenHash");
                CREATE INDEX IF NOT EXISTS "IX_TransformationalLeaderSessions_SolvoId" ON "TransformationalLeaderSessions" ("SolvoId");

                UPDATE "TransformationalLeaderSessions"
                SET "OperationsJson" = CASE
                    WHEN trim("Operation") = '' THEN '[]'
                    ELSE '["' || replace("Operation", '"', '\"') || '"]'
                END
                WHERE "OperationsJson" IS NULL OR "OperationsJson" = '[]';
                """,
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "TransformationalLeaderCandidates" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_TransformationalLeaderCandidates" PRIMARY KEY,
                    "SolvoId" TEXT NOT NULL,
                    "FullName" TEXT NOT NULL,
                    "CorporateEmail" TEXT NOT NULL,
                    "JobTitleCode" TEXT NOT NULL,
                    "Status" TEXT NOT NULL,
                    "Operation" TEXT NOT NULL,
                    "Client" TEXT NOT NULL,
                    "Department" TEXT NOT NULL,
                    "IsActive" INTEGER NOT NULL,
                    "CreatedAtUtc" TEXT NOT NULL,
                    "UpdatedAtUtc" TEXT NOT NULL,
                    "LastSyncedAtUtc" TEXT NOT NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_TransformationalLeaderCandidates_SolvoId" ON "TransformationalLeaderCandidates" ("SolvoId");
                CREATE INDEX IF NOT EXISTS "IX_TransformationalLeaderCandidates_IsActive" ON "TransformationalLeaderCandidates" ("IsActive");
                CREATE INDEX IF NOT EXISTS "IX_TransformationalLeaderCandidates_Operation" ON "TransformationalLeaderCandidates" ("Operation");
                CREATE INDEX IF NOT EXISTS "IX_TransformationalLeaderCandidates_CorporateEmail" ON "TransformationalLeaderCandidates" ("CorporateEmail");
                """,
                cancellationToken);
        }

        await EnsureBootstrapAdminAsync(cancellationToken);
    }

    private async Task EnsureSqliteColumnAsync(string tableName, string columnName, string definition, CancellationToken cancellationToken)
    {
        if (await SqliteColumnExistsAsync(tableName, columnName, cancellationToken))
        {
            return;
        }

        var commandText = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {definition};";
        await dbContext.Database.ExecuteSqlRawAsync(commandText, cancellationToken);
    }

    private async Task EnsureSqliteColumnIfTableExistsAsync(string tableName, string columnName, string definition, CancellationToken cancellationToken)
    {
        if (!await SqliteTableExistsAsync(tableName, cancellationToken))
        {
            return;
        }

        await EnsureSqliteColumnAsync(tableName, columnName, definition, cancellationToken);
    }

    private async Task<bool> SqliteTableExistsAsync(string tableName, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = $tableName;";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "$tableName";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            var scalar = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(scalar) > 0;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<bool> SqliteColumnExistsAsync(string tableName, string columnName, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(1) FROM pragma_table_info(@tableName) WHERE name = @columnName;";

            var tableParameter = command.CreateParameter();
            tableParameter.ParameterName = "@tableName";
            tableParameter.Value = tableName;
            command.Parameters.Add(tableParameter);

            var columnParameter = command.CreateParameter();
            columnParameter.ParameterName = "@columnName";
            columnParameter.Value = columnName;
            command.Parameters.Add(columnParameter);

            var scalar = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(scalar) > 0;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task EnsureBootstrapAdminAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.AdminUsers.AnyAsync(cancellationToken))
        {
            return;
        }

        var email = configuration["PulseCheck:BootstrapAdmin:Email"];
        var password = configuration["PulseCheck:BootstrapAdmin:Password"];
        var displayName = configuration["PulseCheck:BootstrapAdmin:DisplayName"];
        var authenticationMode = configuration["PulseCheck:BootstrapAdmin:AuthenticationMode"] ?? "Local";

        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var normalizedMode = authenticationMode.Trim();
        var passwordHash = string.Empty;
        if (string.Equals(normalizedMode, "Local", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return;
            }

            passwordHash = AdminSecurity.HashPassword(password);
        }
        else if (!string.Equals(normalizedMode, "Entra", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        dbContext.AdminUsers.Add(new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? email.Trim() : displayName.Trim(),
            AuthenticationMode = normalizedMode,
            Role = "Owner",
            PasswordHash = passwordHash,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
