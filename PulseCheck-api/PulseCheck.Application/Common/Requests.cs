using PulseCheck.Domain.Enums;

namespace PulseCheck.Application.Common;

public sealed record CreateCampaignRequest(
    string Name,
    string Audience,
    string ScheduleRule,
    IReadOnlyList<CampaignQuestionRequest>? Questions,
    string? QuestionText,
    TimeOnly DeliveryWindowStart,
    TimeOnly DeliveryWindowEnd,
    string CreatedBy);

public sealed record UpdateCampaignRequest(
    string Name,
    string Audience,
    string ScheduleRule,
    IReadOnlyList<CampaignQuestionRequest>? Questions,
    string? QuestionText,
    TimeOnly DeliveryWindowStart,
    TimeOnly DeliveryWindowEnd);

public sealed record CampaignQuestionRequest(
    Guid? Id,
    string Text,
    CampaignQuestionType Type,
    int? MinValue,
    int? MaxValue,
    string? Placeholder,
    IReadOnlyList<string>? Options);

public sealed record UpdateCampaignStatusRequest(CampaignStatus Status);

public sealed record RegisterDeviceRequest(
    string DeviceId,
    string Hostname,
    string UserId,
    string UserName,
    string Email,
    string Department,
    string OperatingSystem,
    string AgentVersion,
    string? CurrentAgentToken = null);

public sealed record SubmitResponseRequest(
    Guid CampaignId,
    Guid QuestionId,
    string UserId,
    string UserName,
    string Email,
    string Department,
    string DeviceId,
    string Hostname,
    int? NumericValue,
    string? TextValue,
    Guid? SubmissionId,
    DateTimeOffset AnsweredAtUtc);

public sealed record DeliveryLogRequest(
    Guid CampaignId,
    string DeviceId,
    string UserId,
    string UserName,
    string Email,
    string Hostname,
    string Status,
    string? Error,
    int RetryCount,
    DateTimeOffset PromptedAtUtc);

public sealed record AgentActivityEventRequest(
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
    DateTimeOffset? LockedAtUtc = null,
    DateTimeOffset? LockedAtLocal = null,
    DateTimeOffset? OccurredAtLocal = null);

public sealed record AdminLoginRequest(
    string Email,
    string Password);

public sealed record AdminEntraLoginRequest(
    string AccessToken);

public sealed record AdminEntraAuthorizationCodeRequest(
    string Code,
    string State,
    string RedirectUri);

public sealed record TlDashboardRequest(
    IReadOnlyList<string>? WeekIds,
    IReadOnlyList<Guid>? CampaignIds,
    IReadOnlyList<TlQuestionAnswerFilterRequest>? AnswerFilters);

public sealed record TlQuestionAnswerFilterRequest(
    Guid QuestionId,
    IReadOnlyList<string>? Values);

public sealed record CreateAdminUserRequest(
    string Email,
    IReadOnlyList<string>? Roles = null);

public sealed record UpdateAdminUserStatusRequest(
    bool IsActive);

public sealed record UpsertClientInactivityAlertSettingRequest(
    Guid? Id,
    string Client,
    string Operation,
    int AlertThresholdMinutes,
    bool IsEnabled,
    IReadOnlyList<string>? AdditionalRecipientEmails = null);
