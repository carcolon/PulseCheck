using PulseCheck.Domain.Enums;

namespace PulseCheck.Application.Common;

public sealed record CampaignQuestionDto(
    Guid Id,
    string Text,
    CampaignQuestionType Type,
    int? MinValue,
    int? MaxValue,
    string? Placeholder,
    IReadOnlyList<string>? Options);

public sealed record CampaignDto(
    Guid Id,
    string Name,
    string Audience,
    string ScheduleRule,
    TimeOnly DeliveryWindowStart,
    TimeOnly DeliveryWindowEnd,
    CampaignStatus Status,
    IReadOnlyList<CampaignQuestionDto> Questions,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? DeletedAtUtc);

public sealed record RegisterDeviceResponse(Guid Id, string DeviceId, DateTimeOffset RegisteredAtUtc);

public sealed record AgentRegistrationResponse(RegisterDeviceResponse Device, string AgentToken);

public sealed record AgentSyncResponse(RegisterDeviceResponse Device, IReadOnlyList<CampaignDto> ActiveCampaigns, DateTimeOffset GeneratedAtUtc);

public sealed record CampaignAudienceOptionsDto(IReadOnlyList<string> Operations);

public sealed record ClientInactivityAlertOptionsDto(
    IReadOnlyList<string> Clients,
    IReadOnlyList<string> Operations,
    IReadOnlyList<ClientInactivityAlertSettingDto> Settings);

public sealed record ClientInactivityAlertSettingDto(
    Guid Id,
    string Client,
    string Operation,
    int AlertThresholdMinutes,
    bool IsEnabled,
    IReadOnlyList<string> AdditionalRecipientEmails,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record TransformationalLeaderCandidateDto(
    string SolvoId,
    string FullName,
    string CorporateEmail,
    string JobTitleCode,
    string Status,
    string CurrentOperation,
    string Client,
    string Department,
    string AssignedOperation,
    IReadOnlyList<string> AssignedOperations,
    DateTimeOffset? AssignmentUpdatedAtUtc);

public sealed record TransformationalLeaderOptionsDto(
    IReadOnlyList<string> Operations,
    IReadOnlyList<TransformationalLeaderCandidateDto> Leaders);

public sealed record FabricPeopleColumnsDiagnosticsDto(
    DateTimeOffset GeneratedAtUtc,
    string TableName,
    long TotalRows,
    IReadOnlyList<FabricPeopleColumnDiagnosticsDto> Columns);

public sealed record FabricPeopleColumnDiagnosticsDto(
    string Name,
    long NonEmptyRows,
    long DistinctValues,
    IReadOnlyList<string> SampleValues);

public sealed record FabricEmployeeProfileDiagnosticsDto(
    DateTimeOffset GeneratedAtUtc,
    string TableName,
    string Email,
    bool IsConfigured,
    int MatchCount,
    FabricEmployeeProfileDiagnosticRowDto? ResolvedProfile,
    IReadOnlyList<FabricEmployeeProfileDiagnosticRowDto> Rows);

public sealed record FabricEmployeeProfileDiagnosticRowDto(
    string SolvoId,
    string FullName,
    string CorporateEmail,
    string UserPrincipalName,
    string JobTitleCode,
    string Status,
    string Operation,
    string ClientCode,
    string Client,
    string DepartmentCode,
    string Department,
    string LeaderSolvoId,
    string LeaderFullName,
    string LeaderCorporateEmail);

public sealed record PulseResponseDto(
    Guid Id,
    Guid CampaignId,
    Guid QuestionId,
    string QuestionText,
    CampaignQuestionType QuestionType,
    string DeviceId,
    string UserId,
    string UserName,
    string Email,
    string EmployeeId,
    string EntraObjectId,
    string UserPrincipalName,
    string Operation,
    string EmployeeStatus,
    string LeaderSolvoId,
    string LeaderFullName,
    string LeaderCorporateEmail,
    string Department,
    string Hostname,
    int? NumericValue,
    int? MinValue,
    int? MaxValue,
    string? TextValue,
    Guid SubmissionId,
    DateTimeOffset AnsweredAtUtc);

public sealed record ResponseTrendPoint(string Label, double AverageValue, int ResponseCount);

public sealed record DepartmentSnapshot(string Department, double AverageValue, int ResponseCount);

public sealed record DeviceHeartbeatDto(
    string DeviceId,
    string Hostname,
    string UserName,
    string Email,
    string EmployeeId,
    string EntraObjectId,
    string UserPrincipalName,
    string Operation,
    string EmployeeStatus,
    string LeaderSolvoId,
    string LeaderFullName,
    string LeaderCorporateEmail,
    string Client,
    string Department,
    string OperatingSystem,
    string AgentVersion,
    DateTimeOffset LastSeenAtUtc,
    DateTimeOffset? LastSeenAtLocal = null);

public sealed record LiveEventDto(string Type, string Message, DateTimeOffset OccurredAtUtc);

public sealed record DeliveryLogDto(
    Guid Id,
    Guid CampaignId,
    string CampaignName,
    string DeviceId,
    string UserId,
    string UserName,
    string Email,
    string Hostname,
    string Status,
    string? Error,
    int RetryCount,
    DateTimeOffset PromptedAtUtc);

public sealed record AgentActivityEventDto(
    Guid Id,
    string DeviceId,
    string UserId,
    string UserName,
    string Email,
    string Department,
    string Hostname,
    string EventType,
    string? LockReason,
    int? IdleSecondsAtLock,
    int? DurationSeconds,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset? OccurredAtLocal);

public sealed record DashboardSummary(
    int ActiveCampaigns,
    int RegisteredDevices,
    int ResponsesToday,
    double AverageMood,
    IReadOnlyList<ResponseTrendPoint> Trend,
    IReadOnlyList<DepartmentSnapshot> Departments,
    IReadOnlyList<DeviceHeartbeatDto> RecentDevices,
    IReadOnlyList<PulseResponseDto> RecentResponses,
    IReadOnlyList<DeliveryLogDto> RecentDeliveryLogs);

public sealed record DashboardAlertDto(
    string Tone,
    string Eyebrow,
    string Title,
    string Text);

public sealed record DashboardMetricDto(
    string Label,
    string Value,
    string Context,
    string Trend);

public sealed record DashboardPulseTrendPointDto(
    string Day,
    double? Pulse);

public sealed record DashboardDistributionBucketDto(
    string Label,
    int Value);

public sealed record DashboardResponseMixBucketDto(
    string Label,
    int Value,
    double Percentage);

public sealed record DashboardScaleDistributionBucketDto(
    string Label,
    int Value,
    double Percentage);

public sealed record DashboardActionItemDto(
    Guid CampaignId,
    string Title,
    string Detail,
    string Status,
    string ActionLabel);

public sealed record DashboardInsightDto(
    string Tone,
    string Eyebrow,
    string Title,
    string Text);

public sealed record DashboardOverviewDto(
    string HealthTone,
    string HealthLabel,
    bool HasSignal,
    int ActiveCampaigns,
    int RegisteredDevices,
    int ResponsesToday,
    double? AverageMood,
    string? PulseDelta,
    int? ParticipationRate,
    int PendingAlerts,
    string LatestEvent,
    IReadOnlyList<DashboardAlertDto> Alerts,
    IReadOnlyList<DashboardMetricDto> Metrics,
    IReadOnlyList<DashboardPulseTrendPointDto> PulseTrend,
    IReadOnlyList<DashboardResponseMixBucketDto> ResponseMix,
    IReadOnlyList<DashboardScaleDistributionBucketDto> ScaleDistribution,
    int NoResponseCount,
    IReadOnlyList<DashboardActionItemDto> Actions,
    IReadOnlyList<string> RecentActivity,
    DashboardInsightDto Insight);

public sealed record ReportExportData(
    DateOnly FromDate,
    DateOnly ToDate,
    DateTimeOffset GeneratedAtUtc,
    int ActiveCampaigns,
    int RegisteredDevices,
    int TotalResponses,
    int TotalSubmissions,
    int UniqueUsers,
    double AverageMood,
    IReadOnlyList<ReportDailyMetric> DailyMetrics,
    IReadOnlyList<ReportCampaignMetric> CampaignMetrics,
    IReadOnlyList<ReportResponseRow> Responses);

public sealed record ReportDailyMetric(
    DateOnly Date,
    int TotalResponses,
    int TotalSubmissions,
    int NumericResponses,
    int TextResponses,
    double AverageMood);

public sealed record ReportCampaignMetric(
    string CampaignName,
    string Audience,
    string Status,
    int TotalResponses,
    int TotalSubmissions,
    double AverageMood,
    DateTimeOffset UpdatedAtUtc);

public sealed record ReportResponseRow(
    string CampaignName,
    string Audience,
    string QuestionText,
    CampaignQuestionType QuestionType,
    int? NumericValue,
    int? MinValue,
    int? MaxValue,
    string? TextValue,
    string UserName,
    string Email,
    string EmployeeId,
    string PayrollCompany,
    string Country,
    string InternalEmployeeCategory,
    string JobTitle,
    string EntraObjectId,
    string UserPrincipalName,
    string Operation,
    string EmployeeStatus,
    string LeaderSolvoId,
    string LeaderFullName,
    string LeaderCorporateEmail,
    string Department,
    string Hostname,
    string DeviceId,
    Guid SubmissionId,
    DateTimeOffset AnsweredAtUtc);

public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role);

public sealed record AdminAccountDto(
    Guid Id,
    string Email,
    string DisplayName,
    string AuthenticationMode,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastLoginAtUtc);

public sealed record AdminSessionDto(
    string Token,
    string CsrfToken,
    DateTimeOffset ExpiresAtUtc,
    AdminUserDto User,
    string? ReturnToPath = null);

public sealed record TransformationalLeaderSessionDto(
    string Token,
    string CsrfToken,
    DateTimeOffset ExpiresAtUtc,
    AdminUserDto User,
    string SolvoId,
    string Operation,
    IReadOnlyList<string> Operations,
    string? ReturnToPath = null);

public sealed record TlDashboardDto(
    string DisplayName,
    string SolvoId,
    string Operation,
    IReadOnlyList<string> Operations,
    IReadOnlyList<TlWeekOptionDto> Weeks,
    IReadOnlyList<TlCampaignOptionDto> Campaigns,
    IReadOnlyList<TlResponseRowDto> Responses);

public sealed record TlExportJobDto(
    Guid Id,
    string Status,
    string FileName,
    int ResponseCount,
    string? Error,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? DownloadedAtUtc);

public sealed record TlWeekOptionDto(
    string Id,
    string Label,
    DateOnly StartsAt,
    DateOnly EndsAt);

public sealed record TlCampaignOptionDto(
    Guid Id,
    string Name,
    CampaignStatus Status,
    DateTimeOffset? DeletedAtUtc,
    IReadOnlyList<string> WeekIds,
    IReadOnlyList<TlQuestionOptionDto> Questions);

public sealed record TlQuestionOptionDto(
    Guid Id,
    string Text,
    CampaignQuestionType Type,
    int? MinValue,
    int? MaxValue,
    IReadOnlyList<string> Options);

public sealed record TlResponseRowDto(
    Guid Id,
    Guid CampaignId,
    Guid QuestionId,
    string WeekId,
    string CampaignName,
    string QuestionText,
    CampaignQuestionType QuestionType,
    int? NumericValue,
    string? TextValue,
    string UserName,
    string Email,
    string EmployeeId,
    string LeaderSolvoId,
    string LeaderFullName,
    string LeaderCorporateEmail,
    string InternalEmployeeCategory,
    string JobTitle,
    string Operation,
    string EmployeeStatus,
    string Department,
    string Hostname,
    DateTimeOffset AnsweredAtUtc);
