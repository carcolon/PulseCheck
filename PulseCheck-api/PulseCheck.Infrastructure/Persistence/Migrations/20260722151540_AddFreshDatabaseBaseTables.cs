using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulseCheck.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFreshDatabaseBaseTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[Campaigns]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [Campaigns] (
                        [Id] uniqueidentifier NOT NULL,
                        [Name] nvarchar(200) NOT NULL,
                        [Audience] nvarchar(200) NOT NULL,
                        [ScheduleRule] nvarchar(120) NOT NULL,
                        [DeliveryWindowStart] time NOT NULL,
                        [DeliveryWindowEnd] time NOT NULL,
                        [Status] int NOT NULL,
                        [QuestionText] nvarchar(500) NOT NULL,
                        [MinValue] int NOT NULL,
                        [MaxValue] int NOT NULL,
                        [QuestionsJson] nvarchar(max) NOT NULL,
                        [CreatedBy] nvarchar(120) NOT NULL,
                        [CreatedAtUtc] datetimeoffset NOT NULL,
                        [UpdatedAtUtc] datetimeoffset NOT NULL,
                        CONSTRAINT [PK_Campaigns] PRIMARY KEY ([Id])
                    );
                END;

                IF OBJECT_ID(N'[Devices]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [Devices] (
                        [Id] uniqueidentifier NOT NULL,
                        [DeviceId] nvarchar(80) NOT NULL,
                        [Hostname] nvarchar(120) NOT NULL,
                        [UserId] nvarchar(120) NOT NULL,
                        [UserName] nvarchar(160) NOT NULL,
                        [Email] nvarchar(160) NOT NULL,
                        [EmployeeId] nvarchar(80) NOT NULL,
                        [EntraObjectId] nvarchar(80) NOT NULL,
                        [UserPrincipalName] nvarchar(180) NOT NULL,
                        [Operation] nvarchar(180) NOT NULL,
                        [EmployeeStatus] nvarchar(80) NOT NULL,
                        [LeaderSolvoId] nvarchar(80) NOT NULL,
                        [LeaderFullName] nvarchar(180) NOT NULL,
                        [LeaderCorporateEmail] nvarchar(180) NOT NULL,
                        [Client] nvarchar(180) NOT NULL,
                        [Department] nvarchar(120) NOT NULL,
                        [OperatingSystem] nvarchar(120) NOT NULL,
                        [AgentVersion] nvarchar(60) NOT NULL,
                        [FirstSeenAtUtc] datetimeoffset NOT NULL,
                        [LastSeenAtUtc] datetimeoffset NOT NULL,
                        CONSTRAINT [PK_Devices] PRIMARY KEY ([Id])
                    );
                END;

                IF OBJECT_ID(N'[Responses]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [Responses] (
                        [Id] uniqueidentifier NOT NULL,
                        [CampaignId] uniqueidentifier NOT NULL,
                        [QuestionId] uniqueidentifier NOT NULL,
                        [QuestionText] nvarchar(500) NOT NULL,
                        [QuestionType] int NOT NULL,
                        [DeviceId] nvarchar(80) NOT NULL,
                        [UserId] nvarchar(120) NOT NULL,
                        [UserName] nvarchar(160) NOT NULL,
                        [Email] nvarchar(160) NOT NULL,
                        [EmployeeId] nvarchar(80) NOT NULL,
                        [EntraObjectId] nvarchar(80) NOT NULL,
                        [UserPrincipalName] nvarchar(180) NOT NULL,
                        [Operation] nvarchar(180) NOT NULL,
                        [EmployeeStatus] nvarchar(80) NOT NULL,
                        [LeaderSolvoId] nvarchar(80) NOT NULL,
                        [LeaderFullName] nvarchar(180) NOT NULL,
                        [LeaderCorporateEmail] nvarchar(180) NOT NULL,
                        [Department] nvarchar(120) NOT NULL,
                        [Hostname] nvarchar(120) NOT NULL,
                        [Value] int NOT NULL,
                        [NumericValue] int NULL,
                        [TextValue] nvarchar(2000) NULL,
                        [SubmissionId] uniqueidentifier NOT NULL,
                        [AnsweredAtUtc] datetimeoffset NOT NULL,
                        CONSTRAINT [PK_Responses] PRIMARY KEY ([Id])
                    );
                END;

                IF OBJECT_ID(N'[DeliveryLogs]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [DeliveryLogs] (
                        [Id] uniqueidentifier NOT NULL,
                        [CampaignId] uniqueidentifier NOT NULL,
                        [DeviceId] nvarchar(80) NOT NULL,
                        [UserId] nvarchar(120) NOT NULL,
                        [UserName] nvarchar(160) NOT NULL,
                        [Email] nvarchar(160) NOT NULL,
                        [Hostname] nvarchar(120) NOT NULL,
                        [Status] nvarchar(48) NOT NULL,
                        [Error] nvarchar(500) NULL,
                        [RetryCount] int NOT NULL,
                        [PromptedAtUtc] datetimeoffset NOT NULL,
                        CONSTRAINT [PK_DeliveryLogs] PRIMARY KEY ([Id])
                    );
                END;

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
                END;

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
                        CONSTRAINT [PK_AdminSessions] PRIMARY KEY ([Id])
                    );
                END;

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

                IF OBJECT_ID(N'[AgentCredentials]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [AgentCredentials] (
                        [Id] uniqueidentifier NOT NULL,
                        [DeviceId] nvarchar(80) NOT NULL,
                        [TokenHash] nvarchar(64) NOT NULL,
                        [CreatedAtUtc] datetimeoffset NOT NULL,
                        [ExpiresAtUtc] datetimeoffset NOT NULL,
                        [LastUsedAtUtc] datetimeoffset NOT NULL,
                        [RevokedAtUtc] datetimeoffset NULL,
                        CONSTRAINT [PK_AgentCredentials] PRIMARY KEY ([Id])
                    );
                END;

                IF OBJECT_ID(N'[ClientInactivityAlertSettings]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [ClientInactivityAlertSettings] (
                        [Id] uniqueidentifier NOT NULL,
                        [Client] nvarchar(180) NOT NULL,
                        [Operation] nvarchar(180) NOT NULL,
                        [AlertThresholdMinutes] int NOT NULL,
                        [IsEnabled] bit NOT NULL,
                        [AdditionalRecipientEmailsJson] nvarchar(4000) NOT NULL,
                        [CreatedAtUtc] datetimeoffset NOT NULL,
                        [UpdatedAtUtc] datetimeoffset NOT NULL,
                        CONSTRAINT [PK_ClientInactivityAlertSettings] PRIMARY KEY ([Id])
                    );
                END;

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

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AdminUsers_Email' AND [object_id] = OBJECT_ID(N'[AdminUsers]'))
                    CREATE UNIQUE INDEX [IX_AdminUsers_Email] ON [AdminUsers] ([Email]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AdminSessions_AdminUserId' AND [object_id] = OBJECT_ID(N'[AdminSessions]'))
                    CREATE INDEX [IX_AdminSessions_AdminUserId] ON [AdminSessions] ([AdminUserId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AdminSessions_TokenHash' AND [object_id] = OBJECT_ID(N'[AdminSessions]'))
                    CREATE UNIQUE INDEX [IX_AdminSessions_TokenHash] ON [AdminSessions] ([TokenHash]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AgentCredentials_DeviceId' AND [object_id] = OBJECT_ID(N'[AgentCredentials]'))
                    CREATE UNIQUE INDEX [IX_AgentCredentials_DeviceId] ON [AgentCredentials] ([DeviceId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AgentCredentials_TokenHash' AND [object_id] = OBJECT_ID(N'[AgentCredentials]'))
                    CREATE UNIQUE INDEX [IX_AgentCredentials_TokenHash] ON [AgentCredentials] ([TokenHash]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ClientInactivityAlertSettings_Client' AND [object_id] = OBJECT_ID(N'[ClientInactivityAlertSettings]'))
                    CREATE INDEX [IX_ClientInactivityAlertSettings_Client] ON [ClientInactivityAlertSettings] ([Client]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ClientInactivityAlertSettings_Operation' AND [object_id] = OBJECT_ID(N'[ClientInactivityAlertSettings]'))
                    CREATE INDEX [IX_ClientInactivityAlertSettings_Operation] ON [ClientInactivityAlertSettings] ([Operation]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ClientInactivityAlertSettings_Client_Operation_AlertThresholdMinutes' AND [object_id] = OBJECT_ID(N'[ClientInactivityAlertSettings]'))
                    CREATE UNIQUE INDEX [IX_ClientInactivityAlertSettings_Client_Operation_AlertThresholdMinutes] ON [ClientInactivityAlertSettings] ([Client], [Operation], [AlertThresholdMinutes]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DeliveryLogs_CampaignId' AND [object_id] = OBJECT_ID(N'[DeliveryLogs]'))
                    CREATE INDEX [IX_DeliveryLogs_CampaignId] ON [DeliveryLogs] ([CampaignId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Devices_Client' AND [object_id] = OBJECT_ID(N'[Devices]'))
                    CREATE INDEX [IX_Devices_Client] ON [Devices] ([Client]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Devices_DeviceId' AND [object_id] = OBJECT_ID(N'[Devices]'))
                    CREATE UNIQUE INDEX [IX_Devices_DeviceId] ON [Devices] ([DeviceId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Devices_EmployeeId' AND [object_id] = OBJECT_ID(N'[Devices]'))
                    CREATE INDEX [IX_Devices_EmployeeId] ON [Devices] ([EmployeeId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Devices_EntraObjectId' AND [object_id] = OBJECT_ID(N'[Devices]'))
                    CREATE INDEX [IX_Devices_EntraObjectId] ON [Devices] ([EntraObjectId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Devices_LeaderSolvoId' AND [object_id] = OBJECT_ID(N'[Devices]'))
                    CREATE INDEX [IX_Devices_LeaderSolvoId] ON [Devices] ([LeaderSolvoId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Devices_Operation' AND [object_id] = OBJECT_ID(N'[Devices]'))
                    CREATE INDEX [IX_Devices_Operation] ON [Devices] ([Operation]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_LockedSessionAlertNotifications_DeviceId_LockedAtUtc_ThresholdMinutes' AND [object_id] = OBJECT_ID(N'[LockedSessionAlertNotifications]'))
                    CREATE UNIQUE INDEX [IX_LockedSessionAlertNotifications_DeviceId_LockedAtUtc_ThresholdMinutes] ON [LockedSessionAlertNotifications] ([DeviceId], [LockedAtUtc], [ThresholdMinutes]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Responses_CampaignId' AND [object_id] = OBJECT_ID(N'[Responses]'))
                    CREATE INDEX [IX_Responses_CampaignId] ON [Responses] ([CampaignId]);

                IF OBJECT_ID(N'[FK_AdminSessions_AdminUsers_AdminUserId]', N'F') IS NULL
                   AND OBJECT_ID(N'[AdminSessions]', N'U') IS NOT NULL
                   AND OBJECT_ID(N'[AdminUsers]', N'U') IS NOT NULL
                    ALTER TABLE [AdminSessions] ADD CONSTRAINT [FK_AdminSessions_AdminUsers_AdminUserId] FOREIGN KEY ([AdminUserId]) REFERENCES [AdminUsers] ([Id]) ON DELETE CASCADE;

                IF OBJECT_ID(N'[FK_DeliveryLogs_Campaigns_CampaignId]', N'F') IS NULL
                   AND OBJECT_ID(N'[DeliveryLogs]', N'U') IS NOT NULL
                   AND OBJECT_ID(N'[Campaigns]', N'U') IS NOT NULL
                    ALTER TABLE [DeliveryLogs] ADD CONSTRAINT [FK_DeliveryLogs_Campaigns_CampaignId] FOREIGN KEY ([CampaignId]) REFERENCES [Campaigns] ([Id]) ON DELETE CASCADE;

                IF OBJECT_ID(N'[FK_Responses_Campaigns_CampaignId]', N'F') IS NULL
                   AND OBJECT_ID(N'[Responses]', N'U') IS NOT NULL
                   AND OBJECT_ID(N'[Campaigns]', N'U') IS NOT NULL
                    ALTER TABLE [Responses] ADD CONSTRAINT [FK_Responses_Campaigns_CampaignId] FOREIGN KEY ([CampaignId]) REFERENCES [Campaigns] ([Id]) ON DELETE CASCADE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This repair migration is intentionally non-destructive.
        }
    }
}
