using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using PulseCheck.Domain.Entities;
using PulseCheck.Domain.Enums;
namespace PulseCheck.Infrastructure.Persistence;

public sealed class PulseCheckDbContext(DbContextOptions<PulseCheckDbContext> options) : DbContext(options)
{
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<PulseResponse> Responses => Set<PulseResponse>();
    public DbSet<DeliveryLog> DeliveryLogs => Set<DeliveryLog>();
    public DbSet<AgentActivityEvent> AgentActivityEvents => Set<AgentActivityEvent>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<AdminSession> AdminSessions => Set<AdminSession>();
    public DbSet<AgentCredential> AgentCredentials => Set<AgentCredential>();
    public DbSet<ClientInactivityAlertSetting> ClientInactivityAlertSettings => Set<ClientInactivityAlertSetting>();
    public DbSet<TransformationalLeaderAssignment> TransformationalLeaderAssignments => Set<TransformationalLeaderAssignment>();
    public DbSet<TransformationalLeaderCandidateCache> TransformationalLeaderCandidates => Set<TransformationalLeaderCandidateCache>();
    public DbSet<TransformationalLeaderSession> TransformationalLeaderSessions => Set<TransformationalLeaderSession>();
    public DbSet<TransformationalLeaderExportJob> TransformationalLeaderExportJobs => Set<TransformationalLeaderExportJob>();
    public DbSet<LockedSessionAlertNotification> LockedSessionAlertNotifications => Set<LockedSessionAlertNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Campaign>(entity =>
        {
            entity.ToTable("Campaigns");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(200);
            entity.Property(item => item.Audience).HasMaxLength(200);
            entity.Property(item => item.ScheduleRule).HasMaxLength(120);
            entity.Property(item => item.QuestionText).HasMaxLength(500);
            entity.Property(item => item.QuestionsJson);
            entity.Property(item => item.CreatedBy).HasMaxLength(120);
        });

        modelBuilder.Entity<Device>(entity =>
        {
            entity.ToTable("Devices");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.DeviceId).IsUnique();
            entity.Property(item => item.DeviceId).HasMaxLength(80);
            entity.Property(item => item.Hostname).HasMaxLength(120);
            entity.Property(item => item.UserId).HasMaxLength(120);
            entity.Property(item => item.UserName).HasMaxLength(160);
            entity.Property(item => item.Email).HasMaxLength(160);
            entity.Property(item => item.EmployeeId).HasMaxLength(80);
            entity.Property(item => item.EntraObjectId).HasMaxLength(80);
            entity.Property(item => item.UserPrincipalName).HasMaxLength(180);
            entity.Property(item => item.Operation).HasMaxLength(180);
            entity.Property(item => item.EmployeeStatus).HasMaxLength(80);
            entity.Property(item => item.LeaderSolvoId).HasMaxLength(80);
            entity.Property(item => item.LeaderFullName).HasMaxLength(180);
            entity.Property(item => item.LeaderCorporateEmail).HasMaxLength(180);
            entity.Property(item => item.Client).HasMaxLength(180);
            entity.Property(item => item.Department).HasMaxLength(120);
            entity.Property(item => item.OperatingSystem).HasMaxLength(120);
            entity.Property(item => item.AgentVersion).HasMaxLength(60);
            entity.HasIndex(item => item.EmployeeId);
            entity.HasIndex(item => item.EntraObjectId);
            entity.HasIndex(item => item.Operation);
            entity.HasIndex(item => item.Client);
            entity.HasIndex(item => item.LeaderSolvoId);
        });

        modelBuilder.Entity<PulseResponse>(entity =>
        {
            entity.ToTable("Responses");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.DeviceId).HasMaxLength(80);
            entity.Property(item => item.UserId).HasMaxLength(120);
            entity.Property(item => item.UserName).HasMaxLength(160);
            entity.Property(item => item.Email).HasMaxLength(160);
            entity.Property(item => item.EmployeeId).HasMaxLength(80);
            entity.Property(item => item.EntraObjectId).HasMaxLength(80);
            entity.Property(item => item.UserPrincipalName).HasMaxLength(180);
            entity.Property(item => item.Operation).HasMaxLength(180);
            entity.Property(item => item.EmployeeStatus).HasMaxLength(80);
            entity.Property(item => item.LeaderSolvoId).HasMaxLength(80);
            entity.Property(item => item.LeaderFullName).HasMaxLength(180);
            entity.Property(item => item.LeaderCorporateEmail).HasMaxLength(180);
            entity.Property(item => item.Department).HasMaxLength(120);
            entity.Property(item => item.Hostname).HasMaxLength(120);
            entity.Property(item => item.LegacyValue).HasColumnName("Value");
            entity.Property(item => item.QuestionText).HasMaxLength(500);
            entity.Property(item => item.TextValue).HasMaxLength(2000);
            entity.HasOne(item => item.Campaign)
                .WithMany()
                .HasForeignKey(item => item.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeliveryLog>(entity =>
        {
            entity.ToTable("DeliveryLogs");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.DeviceId).HasMaxLength(80);
            entity.Property(item => item.UserId).HasMaxLength(120);
            entity.Property(item => item.UserName).HasMaxLength(160);
            entity.Property(item => item.Email).HasMaxLength(160);
            entity.Property(item => item.Hostname).HasMaxLength(120);
            entity.Property(item => item.Status).HasMaxLength(48);
            entity.Property(item => item.Error).HasMaxLength(500);
            entity.HasOne(item => item.Campaign)
                .WithMany()
                .HasForeignKey(item => item.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgentActivityEvent>(entity =>
        {
            entity.ToTable("AgentActivityEvents");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.DeviceId).HasMaxLength(80);
            entity.Property(item => item.UserId).HasMaxLength(120);
            entity.Property(item => item.UserName).HasMaxLength(160);
            entity.Property(item => item.Email).HasMaxLength(160);
            entity.Property(item => item.Department).HasMaxLength(120);
            entity.Property(item => item.Hostname).HasMaxLength(120);
            entity.Property(item => item.EventType).HasMaxLength(48);
            entity.Property(item => item.LockReason).HasMaxLength(48);
        });

        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.ToTable("AdminUsers");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Email).IsUnique();
            entity.Property(item => item.Email).HasMaxLength(180);
            entity.Property(item => item.DisplayName).HasMaxLength(160);
            entity.Property(item => item.EntraObjectId).HasMaxLength(80);
            entity.Property(item => item.TenantId).HasMaxLength(80);
            entity.Property(item => item.AuthenticationMode).HasMaxLength(32);
            entity.Property(item => item.Role).HasMaxLength(32).HasDefaultValue("Admin");
            entity.Property(item => item.PasswordHash).HasMaxLength(500);
        });

        modelBuilder.Entity<AdminSession>(entity =>
        {
            entity.ToTable("AdminSessions");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.Property(item => item.TokenHash).HasMaxLength(64);
            entity.Property(item => item.UserAgent).HasMaxLength(500);
            entity.Property(item => item.IpAddress).HasMaxLength(120);
            entity.HasOne(item => item.AdminUser)
                .WithMany()
                .HasForeignKey(item => item.AdminUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgentCredential>(entity =>
        {
            entity.ToTable("AgentCredentials");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.DeviceId).IsUnique();
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.Property(item => item.DeviceId).HasMaxLength(80);
            entity.Property(item => item.TokenHash).HasMaxLength(64);
        });

        modelBuilder.Entity<ClientInactivityAlertSetting>(entity =>
        {
            entity.ToTable("ClientInactivityAlertSettings");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Client);
            entity.HasIndex(item => item.Operation);
            entity.HasIndex(item => new { item.Client, item.Operation, item.AlertThresholdMinutes }).IsUnique();
            entity.Property(item => item.Client).HasMaxLength(180);
            entity.Property(item => item.Operation).HasMaxLength(180);
            entity.Property(item => item.AdditionalRecipientEmailsJson).HasMaxLength(4000);
        });

        modelBuilder.Entity<TransformationalLeaderAssignment>(entity =>
        {
            entity.ToTable("TransformationalLeaderAssignments");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.SolvoId).IsUnique();
            entity.HasIndex(item => item.Operation);
            entity.Property(item => item.SolvoId).HasMaxLength(80);
            entity.Property(item => item.Operation).HasMaxLength(180);
            entity.Property(item => item.OperationsJson).HasMaxLength(2000);
        });

        modelBuilder.Entity<TransformationalLeaderCandidateCache>(entity =>
        {
            entity.ToTable("TransformationalLeaderCandidates");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.SolvoId).IsUnique();
            entity.HasIndex(item => item.IsActive);
            entity.HasIndex(item => item.Operation);
            entity.HasIndex(item => item.CorporateEmail);
            entity.Property(item => item.SolvoId).HasMaxLength(80);
            entity.Property(item => item.FullName).HasMaxLength(180);
            entity.Property(item => item.CorporateEmail).HasMaxLength(180);
            entity.Property(item => item.JobTitleCode).HasMaxLength(80);
            entity.Property(item => item.Status).HasMaxLength(80);
            entity.Property(item => item.Operation).HasMaxLength(180);
            entity.Property(item => item.Client).HasMaxLength(180);
            entity.Property(item => item.Department).HasMaxLength(180);
        });

        modelBuilder.Entity<TransformationalLeaderSession>(entity =>
        {
            entity.ToTable("TransformationalLeaderSessions");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.HasIndex(item => item.SolvoId);
            entity.Property(item => item.SolvoId).HasMaxLength(80);
            entity.Property(item => item.Email).HasMaxLength(180);
            entity.Property(item => item.DisplayName).HasMaxLength(180);
            entity.Property(item => item.Operation).HasMaxLength(180);
            entity.Property(item => item.OperationsJson).HasMaxLength(2000);
            entity.Property(item => item.TokenHash).HasMaxLength(64);
            entity.Property(item => item.UserAgent).HasMaxLength(500);
            entity.Property(item => item.IpAddress).HasMaxLength(120);
        });

        modelBuilder.Entity<TransformationalLeaderExportJob>(entity =>
        {
            entity.ToTable("TransformationalLeaderExportJobs");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.SessionId);
            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => item.CreatedAtUtc);
            entity.Property(item => item.SolvoId).HasMaxLength(80);
            entity.Property(item => item.Email).HasMaxLength(180);
            entity.Property(item => item.DisplayName).HasMaxLength(180);
            entity.Property(item => item.Operation).HasMaxLength(180);
            entity.Property(item => item.OperationsJson).HasMaxLength(2000);
            entity.Property(item => item.Status).HasMaxLength(32);
            entity.Property(item => item.FiltersJson).HasMaxLength(4000);
            entity.Property(item => item.HangfireJobId).HasMaxLength(80);
            entity.Property(item => item.FileName).HasMaxLength(240);
            entity.Property(item => item.FilePath).HasMaxLength(1000);
            entity.Property(item => item.Error).HasMaxLength(2000);
            entity.HasOne(item => item.Session)
                .WithMany()
                .HasForeignKey(item => item.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LockedSessionAlertNotification>(entity =>
        {
            entity.ToTable("LockedSessionAlertNotifications");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.DeviceId, item.LockedAtUtc, item.ThresholdMinutes }).IsUnique();
            entity.Property(item => item.DeviceId).HasMaxLength(80);
            entity.Property(item => item.EmployeeId).HasMaxLength(80);
            entity.Property(item => item.Client).HasMaxLength(180);
        });

        Seed(modelBuilder);
    }

    private static void Seed(ModelBuilder modelBuilder)
    {
        var now = new DateTimeOffset(2026, 6, 25, 19, 52, 27, TimeSpan.Zero);

        var dailyCampaignId = Guid.Parse("7f0f8595-44ae-4f1e-ae98-a098ac2bd601");
        var workloadCampaignId = Guid.Parse("5cd9666b-b621-46d9-a2b7-2ae8db4decc8");
        var pilotCampaignId = Guid.Parse("4ec08ed3-257a-40bf-ac31-78cab5bd4fa4");
        var bogotaDeviceId = Guid.Parse("b4da0bf8-aea6-4514-8c12-5183877d7820");
        var medellinDeviceId = Guid.Parse("9eb2924d-c67d-4b4c-8482-b4d2ed6ca1de");
        var dailyQuestionId = Guid.Parse("ca1508ea-1827-4f34-a846-f2f78da30603");
        var workloadQuestionId = Guid.Parse("7470b44d-cf1e-4964-9f7e-4476655f2723");
        var pilotQuestionId = Guid.Parse("dcd5e7df-1ea6-4fc7-b4ad-f983013f4de7");

        modelBuilder.Entity<Campaign>().HasData(
            new Campaign
            {
                Id = dailyCampaignId,
                Name = "Pulso diario de bienestar",
                Audience = "Operaciones y soporte",
                ScheduleRule = "0 0 10 ? * MON-FRI",
                DeliveryWindowStart = new TimeOnly(9, 0),
                DeliveryWindowEnd = new TimeOnly(11, 30),
                Status = CampaignStatus.Active,
                QuestionText = "De 1 a 5, como te sientes hoy?",
                MinValue = 1,
                MaxValue = 5,
                QuestionsJson = CreateQuestionsJson(
                    (dailyQuestionId, "De 1 a 5, como te sientes hoy?", CampaignQuestionType.Scale, 1, 5, null)),
                CreatedBy = "HR Team",
                CreatedAtUtc = now.AddDays(-7),
                UpdatedAtUtc = now.AddDays(-2)
            },
            new Campaign
            {
                Id = workloadCampaignId,
                Name = "Chequeo semanal de carga",
                Audience = "Team leads",
                ScheduleRule = "0 30 15 ? * FRI",
                DeliveryWindowStart = new TimeOnly(14, 0),
                DeliveryWindowEnd = new TimeOnly(16, 0),
                Status = CampaignStatus.Active,
                QuestionText = "Como percibes tu carga de trabajo esta semana?",
                MinValue = 1,
                MaxValue = 5,
                QuestionsJson = CreateQuestionsJson(
                    (workloadQuestionId, "Como percibes tu carga de trabajo esta semana?", CampaignQuestionType.Scale, 1, 5, null)),
                CreatedBy = "People Ops",
                CreatedAtUtc = now.AddDays(-12),
                UpdatedAtUtc = now.AddDays(-5)
            },
            new Campaign
            {
                Id = pilotCampaignId,
                Name = "Piloto liderazgo Medellin",
                Audience = "Leadership Medellin",
                ScheduleRule = "0 0 11 ? * TUE",
                DeliveryWindowStart = new TimeOnly(10, 0),
                DeliveryWindowEnd = new TimeOnly(12, 0),
                Status = CampaignStatus.Draft,
                QuestionText = "Que tan clara fue la comunicacion de esta semana?",
                MinValue = 1,
                MaxValue = 5,
                QuestionsJson = CreateQuestionsJson(
                    (pilotQuestionId, "Que tan clara fue la comunicacion de esta semana?", CampaignQuestionType.Scale, 1, 5, null)),
                CreatedBy = "PMO",
                CreatedAtUtc = now.AddDays(-1),
                UpdatedAtUtc = now.AddHours(-8)
            });

        modelBuilder.Entity<Device>().HasData(
            new Device
            {
                Id = bogotaDeviceId,
                DeviceId = "pc-bog-001",
                Hostname = "PC-BOG-001",
                UserId = "u-001",
                UserName = "Ana Torres",
                Email = "ana.torres@company.com",
                Department = "Operaciones",
                OperatingSystem = "Windows 11",
                AgentVersion = "0.1.0",
                FirstSeenAtUtc = now.AddDays(-7),
                LastSeenAtUtc = now.AddMinutes(-20)
            },
            new Device
            {
                Id = medellinDeviceId,
                DeviceId = "pc-med-021",
                Hostname = "PC-MED-021",
                UserId = "u-002",
                UserName = "Luis Perez",
                Email = "luis.perez@company.com",
                Department = "Soporte",
                OperatingSystem = "Windows 11",
                AgentVersion = "0.1.0",
                FirstSeenAtUtc = now.AddDays(-4),
                LastSeenAtUtc = now.AddMinutes(-12)
            });

        modelBuilder.Entity<PulseResponse>().HasData(
            new PulseResponse
            {
                Id = Guid.Parse("8d6f6046-1aaf-44b4-92a1-7c174a5967cc"),
                CampaignId = dailyCampaignId,
                QuestionId = dailyQuestionId,
                QuestionText = "De 1 a 5, como te sientes hoy?",
                QuestionType = CampaignQuestionType.Scale,
                DeviceId = "pc-bog-001",
                UserId = "u-001",
                UserName = "Ana Torres",
                Email = "ana.torres@company.com",
                Department = "Operaciones",
                Hostname = "PC-BOG-001",
                LegacyValue = 4,
                NumericValue = 4,
                SubmissionId = Guid.Parse("bcfdceff-241e-417f-8487-3f6708e0f9f8"),
                AnsweredAtUtc = now.AddHours(-2)
            },
            new PulseResponse
            {
                Id = Guid.Parse("7058e3cc-412b-41fd-b284-d9dcc385943e"),
                CampaignId = dailyCampaignId,
                QuestionId = dailyQuestionId,
                QuestionText = "De 1 a 5, como te sientes hoy?",
                QuestionType = CampaignQuestionType.Scale,
                DeviceId = "pc-med-021",
                UserId = "u-002",
                UserName = "Luis Perez",
                Email = "luis.perez@company.com",
                Department = "Soporte",
                Hostname = "PC-MED-021",
                LegacyValue = 5,
                NumericValue = 5,
                SubmissionId = Guid.Parse("4b45881c-e9ea-4e71-b0df-e4eb5e17f770"),
                AnsweredAtUtc = now.AddDays(-1)
            });

        modelBuilder.Entity<DeliveryLog>().HasData(
            new DeliveryLog
            {
                Id = Guid.Parse("441f71d5-2689-4387-bd91-177344670e1c"),
                CampaignId = dailyCampaignId,
                DeviceId = "pc-bog-001",
                UserId = "u-001",
                UserName = "Ana Torres",
                Email = "ana.torres@company.com",
                Hostname = "PC-BOG-001",
                Status = "Answered",
                RetryCount = 0,
                PromptedAtUtc = now.AddHours(-2)
            },
            new DeliveryLog
            {
                Id = Guid.Parse("f4ddd0f9-48b2-4304-8f9f-3eae66b25456"),
                CampaignId = workloadCampaignId,
                DeviceId = "pc-med-021",
                UserId = "u-002",
                UserName = "Luis Perez",
                Email = "luis.perez@company.com",
                Hostname = "PC-MED-021",
                Status = "Prompted",
                RetryCount = 0,
                PromptedAtUtc = now.AddMinutes(-35)
            });
    }

    private static string CreateQuestionsJson(params (Guid Id, string Text, CampaignQuestionType Type, int? MinValue, int? MaxValue, string? Placeholder)[] questions)
    {
        var payload = questions.Select(item => new
        {
            id = item.Id,
            text = item.Text,
            type = item.Type,
            minValue = item.MinValue,
            maxValue = item.MaxValue,
            placeholder = item.Placeholder
        });

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return JsonSerializer.Serialize(payload, options);
    }
}
