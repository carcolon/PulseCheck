namespace PulseCheck.Agent;

public sealed record AgentCampaignConfiguration(
    Guid Id,
    string Name,
    string Audience,
    string ScheduleRule,
    string DeliveryWindowStart,
    string DeliveryWindowEnd,
    string Status,
    IReadOnlyList<AgentQuestion> Questions,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record AgentQuestion(
    Guid Id,
    string Text,
    string Type,
    int? MinValue,
    int? MaxValue,
    string? Placeholder,
    IReadOnlyList<string>? Options);

public sealed record PromptAnswer(
    Guid QuestionId,
    string QuestionText,
    string QuestionType,
    int? NumericValue,
    string? TextValue);

public sealed record PromptResult(
    IReadOnlyList<PromptAnswer>? Answers,
    TimeSpan? PostponeFor);

public sealed record RegisteredDevice(
    Guid Id,
    string DeviceId,
    DateTimeOffset RegisteredAtUtc);

public sealed record AgentRegistrationResponse(
    RegisteredDevice Device,
    string AgentToken);

public sealed record AgentSyncResponse(
    RegisteredDevice Device,
    IReadOnlyList<AgentCampaignConfiguration> ActiveCampaigns,
    DateTimeOffset GeneratedAtUtc);

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

public sealed record PendingResponse(
    Guid CampaignId,
    Guid QuestionId,
    string QuestionText,
    string QuestionType,
    string UserId,
    string UserName,
    string Email,
    string Department,
    string DeviceId,
    string Hostname,
    int? NumericValue,
    string? TextValue,
    Guid SubmissionId,
    DateTimeOffset AnsweredAtUtc)
{
    public static IReadOnlyList<PendingResponse> CreateFrom(AgentCampaignConfiguration campaign, AgentRuntimeIdentity identity)
    {
        var submissionId = Guid.NewGuid();
        var answeredAtUtc = DateTimeOffset.UtcNow;
        var answers = new List<PendingResponse>();

        foreach (var question in campaign.Questions)
        {
            if (string.Equals(question.Type, "Text", StringComparison.OrdinalIgnoreCase))
            {
                answers.Add(new PendingResponse(
                    campaign.Id,
                    question.Id,
                    question.Text,
                    question.Type,
                    identity.UserId,
                    identity.UserName,
                    identity.Email,
                    identity.Department,
                    identity.DeviceId,
                    identity.Hostname,
                    null,
                    "Respuesta simulada",
                    submissionId,
                    answeredAtUtc));
            }
            else if (string.Equals(question.Type, "YesNo", StringComparison.OrdinalIgnoreCase))
            {
                var answer = Random.Shared.Next(0, 2) == 0 ? "No" : "Sí";
                answers.Add(new PendingResponse(
                    campaign.Id,
                    question.Id,
                    question.Text,
                    question.Type,
                    identity.UserId,
                    identity.UserName,
                    identity.Email,
                    identity.Department,
                    identity.DeviceId,
                    identity.Hostname,
                    null,
                    answer,
                    submissionId,
                    answeredAtUtc));
            }
            else if (string.Equals(question.Type, "Choice", StringComparison.OrdinalIgnoreCase))
            {
                var options = question.Options?.Where(item => !string.IsNullOrWhiteSpace(item)).ToArray() ?? [];
                var answer = options.Length == 0 ? "Opcion simulada" : options[Random.Shared.Next(0, options.Length)];
                answers.Add(new PendingResponse(
                    campaign.Id,
                    question.Id,
                    question.Text,
                    question.Type,
                    identity.UserId,
                    identity.UserName,
                    identity.Email,
                    identity.Department,
                    identity.DeviceId,
                    identity.Hostname,
                    null,
                    answer,
                    submissionId,
                    answeredAtUtc));
            }
            else
            {
                var min = question.MinValue ?? 1;
                var max = question.MaxValue ?? 5;
                answers.Add(new PendingResponse(
                    campaign.Id,
                    question.Id,
                    question.Text,
                    question.Type,
                    identity.UserId,
                    identity.UserName,
                    identity.Email,
                    identity.Department,
                    identity.DeviceId,
                    identity.Hostname,
                    Random.Shared.Next(min, max + 1),
                    null,
                    submissionId,
                    answeredAtUtc));
            }
        }

        return answers;
    }
}

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
