using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Net.Mail;
using PulseCheck.Application.Common;
using PulseCheck.Application.Ports;
using PulseCheck.Application.Security;
using PulseCheck.Domain.Abstractions;
using PulseCheck.Domain.Entities;
using PulseCheck.Domain.Enums;

namespace PulseCheck.Application.Services;

public sealed class PulseCheckService(
    IPulseCheckUnitOfWork unitOfWork,
    INotificationPublisher notificationPublisher,
    IDateTimeProvider dateTimeProvider,
    IEmployeeIdentityResolver employeeIdentityResolver,
    IEmployeeOperationsProfileResolver employeeOperationsProfileResolver,
    ILeaderAlertEmailService leaderAlertEmailService)
{
    private const string AllOperationsAudience = "Todas las operaciones";
    private const int MinClientAlertThresholdMinutes = 1;
    private const int MaxClientAlertThresholdMinutes = 240;

    private static readonly JsonSerializerOptions QuestionSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<IReadOnlyList<CampaignDto>> GetCampaignsAsync(bool includeDeleted, CancellationToken cancellationToken)
    {
        var campaigns = await unitOfWork.GetCampaignsAsync(cancellationToken);
        return campaigns
            .Where(item => includeDeleted || item.DeletedAtUtc is null)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => item.ToDto())
            .ToArray();
    }

    public async Task<CampaignAudienceOptionsDto> GetCampaignAudienceOptionsAsync(CancellationToken cancellationToken)
    {
        var fabricOperations = await employeeOperationsProfileResolver.GetOperationsAsync(cancellationToken);
        if (fabricOperations.Count > 0)
        {
            return new CampaignAudienceOptionsDto(fabricOperations);
        }

        var devices = await unitOfWork.GetDevicesAsync(cancellationToken);
        var operations = devices
            .Select(item => item.Operation)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item)
            .ToArray();

        return new CampaignAudienceOptionsDto(operations);
    }

    public async Task<ClientInactivityAlertOptionsDto> GetClientInactivityAlertOptionsAsync(CancellationToken cancellationToken)
    {
        var fabricClients = await employeeOperationsProfileResolver.GetClientsAsync(cancellationToken);
        var fabricOperations = await employeeOperationsProfileResolver.GetOperationsAsync(cancellationToken);
        var clients = fabricClients.Count > 0
            ? fabricClients
            : (await unitOfWork.GetDevicesAsync(cancellationToken))
                .Select(item => item.Client)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item)
                .ToArray();

        var operations = fabricOperations.Count > 0
            ? fabricOperations
            : (await unitOfWork.GetDevicesAsync(cancellationToken))
                .Select(item => item.Operation)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item)
                .ToArray();

        var settings = await unitOfWork.GetClientInactivityAlertSettingsAsync(cancellationToken);
        return new ClientInactivityAlertOptionsDto(
            clients,
            operations,
            settings.Select(item => item.ToDto()).ToArray());
    }

    public async Task<ClientInactivityAlertSettingDto> UpsertClientInactivityAlertSettingAsync(
        UpsertClientInactivityAlertSettingRequest request,
        CancellationToken cancellationToken)
    {
        var client = NormalizePlainText(request.Client, 180);
        var operation = NormalizePlainText(request.Operation, 180);
        var additionalRecipientEmails = NormalizeAdditionalRecipientEmails(request.AdditionalRecipientEmails);
        if (client.Length == 0 && operation.Length == 0)
        {
            throw new InvalidOperationException("Selecciona al menos un cliente o una operacion para la regla.");
        }

        var threshold = Math.Clamp(request.AlertThresholdMinutes, MinClientAlertThresholdMinutes, MaxClientAlertThresholdMinutes);
        var setting = request.Id.HasValue
            ? await unitOfWork.GetClientInactivityAlertSettingByIdAsync(request.Id.Value, cancellationToken)
            : null;
        var duplicateExists = await unitOfWork.ClientInactivityAlertSettingExistsAsync(
            client,
            operation,
            threshold,
            setting?.Id,
            cancellationToken);
        if (duplicateExists)
        {
            throw new InvalidOperationException("Ya existe una regla para ese alcance con el mismo tiempo de inactividad.");
        }

        var now = dateTimeProvider.UtcNow;

        if (setting is null)
        {
            setting = new ClientInactivityAlertSetting
            {
                Id = Guid.NewGuid(),
                Client = client,
                Operation = operation,
                CreatedAtUtc = now
            };
            await unitOfWork.AddClientInactivityAlertSettingAsync(setting, cancellationToken);
        }

        setting.Client = client;
        setting.Operation = operation;
        setting.AlertThresholdMinutes = threshold;
        setting.IsEnabled = request.IsEnabled;
        setting.AdditionalRecipientEmailsJson = SerializeAdditionalRecipientEmails(additionalRecipientEmails);
        setting.UpdatedAtUtc = now;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return setting.ToDto();
    }

    public async Task<bool> DeleteClientInactivityAlertSettingAsync(Guid id, CancellationToken cancellationToken)
    {
        var setting = await unitOfWork.GetClientInactivityAlertSettingByIdAsync(id, cancellationToken);
        if (setting is null)
        {
            return false;
        }

        await unitOfWork.RemoveClientInactivityAlertSettingAsync(setting, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CampaignDto> CreateCampaignAsync(CreateCampaignRequest request, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var questions = ResolveQuestionsForRequest(request.Questions, request.QuestionText);
        var firstScaleQuestion = questions.FirstOrDefault(item => item.Type == CampaignQuestionType.Scale);

        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Name = NormalizeRequiredPlainText(request.Name, 200, "Campaign name"),
            Audience = NormalizeAudience(request.Audience),
            ScheduleRule = NormalizeRequiredPlainText(request.ScheduleRule, 120, "Campaign schedule"),
            DeliveryWindowStart = request.DeliveryWindowStart,
            DeliveryWindowEnd = request.DeliveryWindowEnd,
            Status = CampaignStatus.Draft,
            QuestionText = questions[0].Text,
            MinValue = firstScaleQuestion?.MinValue ?? 1,
            MaxValue = firstScaleQuestion?.MaxValue ?? 5,
            QuestionsJson = SerializeQuestions(questions),
            CreatedBy = string.IsNullOrWhiteSpace(request.CreatedBy)
                ? "Admin panel"
                : NormalizeRequiredPlainText(request.CreatedBy, 120, "Campaign creator"),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await unitOfWork.AddCampaignAsync(campaign, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = campaign.ToDto();
        await notificationPublisher.PublishCampaignCreatedAsync(dto, cancellationToken);
        return dto;
    }

    public async Task<CampaignDto?> UpdateCampaignStatusAsync(Guid id, UpdateCampaignStatusRequest request, CancellationToken cancellationToken)
    {
        var campaign = await unitOfWork.GetCampaignByIdAsync(id, cancellationToken);
        if (campaign is null || campaign.DeletedAtUtc is not null)
        {
            return null;
        }

        campaign.Status = request.Status;
        campaign.UpdatedAtUtc = dateTimeProvider.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = campaign.ToDto();
        await notificationPublisher.PublishCampaignUpdatedAsync(dto, cancellationToken);
        return dto;
    }

    public async Task<CampaignDto?> UpdateCampaignAsync(Guid id, UpdateCampaignRequest request, CancellationToken cancellationToken)
    {
        var campaign = await unitOfWork.GetCampaignByIdAsync(id, cancellationToken);
        if (campaign is null || campaign.DeletedAtUtc is not null)
        {
            return null;
        }

        var questions = ResolveQuestionsForRequest(request.Questions, request.QuestionText);
        var firstScaleQuestion = questions.FirstOrDefault(item => item.Type == CampaignQuestionType.Scale);

        campaign.Name = NormalizeRequiredPlainText(request.Name, 200, "Campaign name");
        campaign.Audience = NormalizeAudience(request.Audience);
        campaign.ScheduleRule = NormalizeRequiredPlainText(request.ScheduleRule, 120, "Campaign schedule");
        campaign.QuestionText = questions[0].Text;
        campaign.MinValue = firstScaleQuestion?.MinValue ?? 1;
        campaign.MaxValue = firstScaleQuestion?.MaxValue ?? 5;
        campaign.QuestionsJson = SerializeQuestions(questions);
        campaign.DeliveryWindowStart = request.DeliveryWindowStart;
        campaign.DeliveryWindowEnd = request.DeliveryWindowEnd;
        campaign.UpdatedAtUtc = dateTimeProvider.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        var dto = campaign.ToDto();
        await notificationPublisher.PublishCampaignUpdatedAsync(dto, cancellationToken);
        return dto;
    }

    public async Task<bool> DeleteCampaignAsync(Guid id, CancellationToken cancellationToken)
    {
        var campaign = await unitOfWork.GetCampaignByIdAsync(id, cancellationToken);
        if (campaign is null || campaign.DeletedAtUtc is not null)
        {
            return false;
        }

        campaign.DeletedAtUtc = dateTimeProvider.UtcNow;
        campaign.UpdatedAtUtc = campaign.DeletedAtUtc.Value;
        campaign.Status = CampaignStatus.Paused;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notificationPublisher.PublishLiveEventAsync(
            new LiveEventDto("campaign", $"Campaña eliminada: {campaign.Name}", dateTimeProvider.UtcNow),
            cancellationToken);
        return true;
    }

    public async Task<RegisterDeviceResponse?> RegisterDeviceAsync(RegisterDeviceRequest request, CancellationToken cancellationToken)
    {
        var device = await UpsertDeviceAsync(request, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notificationPublisher.PublishDeviceHeartbeatAsync(device.ToHeartbeatDto(), cancellationToken);
        return device.ToRegisterDto();
    }

    public async Task<AgentSyncResponse?> SyncAgentAsync(RegisterDeviceRequest request, CancellationToken cancellationToken)
    {
        var device = await UpsertDeviceAsync(request, cancellationToken);
        var campaigns = await unitOfWork.GetActiveCampaignsAsync(cancellationToken);
        var knownOperations = await ResolveKnownOperationsAsync(cancellationToken);
        AddKnownOperation(knownOperations, device.Operation);
        var targetedCampaigns = campaigns
            .Where(item => CampaignMatchesDeviceOperation(item, device.Operation, knownOperations))
            .ToArray();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationPublisher.PublishDeviceHeartbeatAsync(device.ToHeartbeatDto(), cancellationToken);
        await notificationPublisher.PublishLiveEventAsync(
            new LiveEventDto("device", $"{device.UserName} activo en {device.Hostname}", device.LastSeenAtUtc),
            cancellationToken);

        return new AgentSyncResponse(
            device.ToRegisterDto(),
            targetedCampaigns.OrderByDescending(item => item.UpdatedAtUtc).Select(item => item.ToDto()).ToArray(),
            dateTimeProvider.UtcNow);
    }

    public async Task<PulseResponseDto?> SubmitResponseAsync(SubmitResponseRequest request, CancellationToken cancellationToken)
    {
        var campaign = await unitOfWork.GetCampaignByIdAsync(request.CampaignId, cancellationToken);
        if (campaign is null || campaign.DeletedAtUtc is not null)
        {
            return null;
        }

        var campaignQuestions = DeserializeQuestions(campaign);
        if (campaignQuestions.Count == 0)
        {
            return null;
        }

        var question = campaignQuestions.FirstOrDefault(item => item.Id == request.QuestionId);
        if (question is null && campaignQuestions.Count == 1)
        {
            question = campaignQuestions[0];
        }

        if (question is null)
        {
            return null;
        }

        int? numericValue = null;
        string? textValue = null;
        if (question.Type == CampaignQuestionType.Scale)
        {
            var min = question.MinValue ?? 1;
            var max = question.MaxValue ?? 5;
            if (request.NumericValue is null || request.NumericValue < min || request.NumericValue > max)
            {
                return null;
            }

            numericValue = request.NumericValue;
        }
        else if (question.Type == CampaignQuestionType.YesNo)
        {
            textValue = request.TextValue?.Trim();
            if (!string.Equals(textValue, "Sí", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(textValue, "Si", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(textValue, "No", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }
        else if (question.Type == CampaignQuestionType.Choice)
        {
            var options = NormalizeChoiceOptions(question.Options);
            textValue = request.TextValue?.Trim();
            if (string.IsNullOrWhiteSpace(textValue))
            {
                return null;
            }

            var selectedOption = options.FirstOrDefault(item => string.Equals(item, textValue, StringComparison.OrdinalIgnoreCase));
            if (selectedOption is null)
            {
                return null;
            }

            textValue = selectedOption;
        }
        else
        {
            if (!PlainTextSecurity.TryNormalize(request.TextValue, 2000, out var normalizedTextValue))
            {
                return null;
            }

            textValue = normalizedTextValue;
        }

        var submissionId = request.SubmissionId is { } value && value != Guid.Empty
            ? value
            : Guid.NewGuid();

        var device = await unitOfWork.GetDeviceByDeviceIdAsync(request.DeviceId, cancellationToken);
        if (device is null)
        {
            device = new Device
            {
                Id = Guid.NewGuid(),
                DeviceId = request.DeviceId,
                FirstSeenAtUtc = dateTimeProvider.UtcNow,
                LastSeenAtUtc = dateTimeProvider.UtcNow
            };
            await unitOfWork.AddDeviceAsync(device, cancellationToken);
        }

        await ApplyDeviceIdentityAsync(
            device,
            new RegisterDeviceRequest(
                request.DeviceId,
                request.Hostname,
                request.UserId,
                request.UserName,
                request.Email,
                request.Department,
                device.OperatingSystem,
                device.AgentVersion),
            cancellationToken);
        device.LastSeenAtUtc = dateTimeProvider.UtcNow;

        var knownOperations = await ResolveKnownOperationsAsync(cancellationToken);
        AddKnownOperation(knownOperations, device.Operation);
        if (!CampaignMatchesDeviceOperation(campaign, device.Operation, knownOperations))
        {
            return null;
        }

        var response = new PulseResponse
        {
            Id = Guid.NewGuid(),
            CampaignId = request.CampaignId,
            QuestionId = question.Id,
            QuestionText = question.Text,
            QuestionType = question.Type,
            DeviceId = request.DeviceId,
            UserId = device.UserId,
            UserName = device.UserName,
            Email = device.Email,
            EmployeeId = device.EmployeeId,
            EntraObjectId = device.EntraObjectId,
            UserPrincipalName = device.UserPrincipalName,
            Operation = device.Operation,
            EmployeeStatus = device.EmployeeStatus,
            LeaderSolvoId = device.LeaderSolvoId,
            LeaderFullName = device.LeaderFullName,
            LeaderCorporateEmail = device.LeaderCorporateEmail,
            Department = device.Department,
            Hostname = request.Hostname,
            LegacyValue = numericValue ?? 0,
            NumericValue = numericValue,
            TextValue = textValue,
            SubmissionId = submissionId,
            AnsweredAtUtc = request.AnsweredAtUtc
        };

        await unitOfWork.AddResponseAsync(response, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = response.ToDto((question.MinValue, question.MaxValue));
        await notificationPublisher.PublishResponseReceivedAsync(dto, cancellationToken);
        await notificationPublisher.PublishLiveEventAsync(
            new LiveEventDto("response", $"{dto.UserName} respondio {FormatAnswerForLiveEvent(dto)} en {dto.Hostname}", dto.AnsweredAtUtc),
            cancellationToken);
        return dto;
    }

    public async Task<IReadOnlyList<PulseResponseDto>> GetRecentResponsesAsync(CancellationToken cancellationToken)
    {
        var responses = await unitOfWork.GetRecentResponsesAsync(24, cancellationToken);
        var campaigns = await unitOfWork.GetCampaignsAsync(cancellationToken);
        var campaignLookup = campaigns.ToDictionary(item => item.Id);
        return responses.Select(item => item.ToDto(GetQuestionBounds(item, campaignLookup))).ToArray();
    }

    public async Task<IReadOnlyList<PulseResponseDto>> GetResponsesAsync(CancellationToken cancellationToken)
    {
        var responses = await unitOfWork.GetResponsesAsync(cancellationToken);
        var campaigns = await unitOfWork.GetCampaignsAsync(cancellationToken);
        var campaignLookup = campaigns.ToDictionary(item => item.Id);

        return responses
            .OrderByDescending(item => item.AnsweredAtUtc)
            .Select(item => item.ToDto(GetQuestionBounds(item, campaignLookup)))
            .ToArray();
    }

    public async Task<IReadOnlyList<DeviceHeartbeatDto>> GetDevicesAsync(CancellationToken cancellationToken)
    {
        var devices = await unitOfWork.GetDevicesAsync(cancellationToken);
        var recentLocalEvents = await unitOfWork.GetRecentAgentActivityEventsAsync(
            5000,
            null,
            null,
            cancellationToken);
        var localOffsetsByDevice = recentLocalEvents
            .Where(item => item.OccurredAtLocal.HasValue)
            .GroupBy(GetActivityDeviceKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                item => item.Key,
                item => item.OrderByDescending(activity => activity.OccurredAtUtc).First().OccurredAtLocal!.Value.Offset,
                StringComparer.OrdinalIgnoreCase);

        return devices
            .OrderByDescending(item => item.LastSeenAtUtc)
            .Select(item => item.ToHeartbeatDto(ResolveLastSeenAtLocal(item, localOffsetsByDevice)))
            .ToArray();
    }

    public async Task<DeliveryLogDto?> CreateDeliveryLogAsync(DeliveryLogRequest request, CancellationToken cancellationToken)
    {
        var campaign = await unitOfWork.GetCampaignByIdAsync(request.CampaignId, cancellationToken);
        if (campaign is null || campaign.DeletedAtUtc is not null)
        {
            return null;
        }

        var log = new DeliveryLog
        {
            Id = Guid.NewGuid(),
            CampaignId = request.CampaignId,
            Campaign = campaign,
            DeviceId = request.DeviceId,
            UserId = request.UserId,
            UserName = request.UserName,
            Email = request.Email,
            Hostname = request.Hostname,
            Status = request.Status,
            Error = request.Error,
            RetryCount = request.RetryCount,
            PromptedAtUtc = request.PromptedAtUtc
        };

        await unitOfWork.AddDeliveryLogAsync(log, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = log.ToDto();
        await notificationPublisher.PublishDeliveryLogAsync(dto, cancellationToken);
        await notificationPublisher.PublishLiveEventAsync(
            new LiveEventDto("delivery", $"{dto.CampaignName} -> {dto.Status} en {dto.Hostname}", dto.PromptedAtUtc),
            cancellationToken);
        return dto;
    }

    public async Task<IReadOnlyList<DeliveryLogDto>> GetDeliveryLogsAsync(CancellationToken cancellationToken)
    {
        var logs = await unitOfWork.GetRecentDeliveryLogsAsync(30, cancellationToken);
        return logs.Select(item => item.ToDto()).ToArray();
    }

    public async Task<IReadOnlyList<AgentActivityEventDto>> GetRecentAgentActivityEventsAsync(CancellationToken cancellationToken)
    {
        var events = await unitOfWork.GetRecentAgentActivityEventsAsync(
            5000,
            dateTimeProvider.UtcNow.AddHours(-48),
            null,
            cancellationToken);
        return events
            .Where(item =>
                string.Equals(item.EventType, "SessionLocked", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.EventType, "SessionUnlocked", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.EventType, "DeviceSuspended", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.EventType, "DeviceResumed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.EventType, "DeviceStarted", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.EventType, "DeviceShutdown", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.ToDto())
            .ToArray();
    }

    public async Task<bool> TrackAgentActivityAsync(AgentActivityEventRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId) ||
            string.IsNullOrWhiteSpace(request.UserId) ||
            string.IsNullOrWhiteSpace(request.EventType))
        {
            return false;
        }

        var eventType = request.EventType.Trim();
        var isLockedAlertCandidate = IsLockedAlertCandidate(eventType);
        Device? activityDevice = null;
        if (isLockedAlertCandidate)
        {
            activityDevice = await EnsureDeviceProfileForActivityAsync(request, cancellationToken);
        }

        if (string.Equals(eventType, "SessionLockedDurationObserved", StringComparison.OrdinalIgnoreCase))
        {
            await SendLockedDeviceLeaderAlertIfNeededAsync(activityDevice, null, request, cancellationToken);
            return true;
        }

        var activityEvent = new AgentActivityEvent
        {
            Id = Guid.NewGuid(),
            DeviceId = request.DeviceId.Trim(),
            UserId = request.UserId.Trim(),
            UserName = request.UserName.Trim(),
            Email = request.Email.Trim(),
            Department = request.Department.Trim(),
            Hostname = request.Hostname.Trim(),
            EventType = eventType,
            LockReason = string.IsNullOrWhiteSpace(request.LockReason) ? null : request.LockReason.Trim(),
            IdleSecondsAtLock = request.IdleSecondsAtLock,
            DurationSeconds = request.DurationSeconds,
            OccurredAtUtc = request.OccurredAtUtc,
            OccurredAtLocal = request.OccurredAtLocal
        };

        await unitOfWork.AddAgentActivityEventAsync(activityEvent, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var liveMessage = request.EventType.Trim() switch
        {
            "SessionLocked" => $"{activityEvent.UserName} bloqueo {activityEvent.Hostname} ({FormatActivityReason(activityEvent.LockReason, activityEvent.IdleSecondsAtLock)}).",
            "SessionLockedThresholdReached" => $"{activityEvent.UserName} lleva {FormatDuration(activityEvent.DurationSeconds ?? 1800)} con {activityEvent.Hostname} bloqueado.",
            "SessionUnlocked" when activityEvent.DurationSeconds is int durationSeconds
                => $"{activityEvent.UserName} desbloqueo {activityEvent.Hostname} tras {FormatDuration(durationSeconds)}.",
            "SessionUnlocked" => $"{activityEvent.UserName} desbloqueo {activityEvent.Hostname}.",
            "DeviceSuspended" => $"{activityEvent.UserName} puso {activityEvent.Hostname} en suspension ({FormatSuspendContext(activityEvent.IdleSecondsAtLock)}).",
            "DeviceResumed" when activityEvent.DurationSeconds is int suspendedSeconds
                => $"{activityEvent.UserName} reanudo {activityEvent.Hostname} tras {FormatDuration(suspendedSeconds)} en suspension.",
            "DeviceResumed" => $"{activityEvent.UserName} reanudo {activityEvent.Hostname}.",
            _ => $"{activityEvent.UserName} registro actividad {activityEvent.EventType} en {activityEvent.Hostname}."
        };

        await notificationPublisher.PublishLiveEventAsync(
            new LiveEventDto("activity", liveMessage, activityEvent.OccurredAtUtc),
            cancellationToken);
        await notificationPublisher.PublishAgentActivityAsync(activityEvent.ToDto(), cancellationToken);
        await SendLockedDeviceLeaderAlertIfNeededAsync(activityDevice, activityEvent, request, cancellationToken);

        return true;
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken)
    {
        var today = dateTimeProvider.UtcNow.ToLocalTime().Date;
        var campaigns = await unitOfWork.GetCampaignsAsync(cancellationToken);
        var devices = await unitOfWork.GetDevicesAsync(cancellationToken);
        var responses = await unitOfWork.GetResponsesAsync(cancellationToken);
        var deliveryLogs = await unitOfWork.GetRecentDeliveryLogsAsync(8, cancellationToken);
        var scaleResponses = responses
            .Where(item => item.QuestionType == CampaignQuestionType.Scale && item.NumericValue.HasValue)
            .ToArray();

        var trend = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var day = today.AddDays(-offset);
                var dayResponses = scaleResponses.Where(item => item.AnsweredAtUtc.ToLocalTime().Date == day).ToArray();
                return new ResponseTrendPoint(
                    day.ToString("MM-dd"),
                    dayResponses.Length == 0 ? 0 : Math.Round(dayResponses.Average(item => item.NumericValue!.Value), 2),
                    dayResponses.Length);
            })
            .Reverse()
            .ToArray();

        var departments = scaleResponses
            .GroupBy(item => item.Department)
            .Select(group => new DepartmentSnapshot(group.Key, Math.Round(group.Average(item => item.NumericValue!.Value), 2), group.Count()))
            .OrderByDescending(item => item.ResponseCount)
            .ToArray();

        var orderedDevices = devices.OrderByDescending(item => item.LastSeenAtUtc).ToArray();
        var orderedResponses = responses.OrderByDescending(item => item.AnsweredAtUtc).ToArray();
        var responsesToday = responses
            .Where(item => item.AnsweredAtUtc.ToLocalTime().Date == today)
            .Select(item => item.SubmissionId == Guid.Empty ? item.Id : item.SubmissionId)
            .Distinct()
            .Count();

        return new DashboardSummary(
            campaigns.Count(item => item.DeletedAtUtc is null && item.Status == CampaignStatus.Active),
            devices.Count,
            responsesToday,
            scaleResponses.Length == 0 ? 0 : Math.Round(scaleResponses.Average(item => item.NumericValue!.Value), 2),
            trend,
            departments,
            orderedDevices.Take(5).Select(item => item.ToHeartbeatDto()).ToArray(),
            orderedResponses.Take(12).Select(item => item.ToDto(GetQuestionBounds(item, campaigns.ToDictionary(campaign => campaign.Id)))).ToArray(),
            deliveryLogs.Select(item => item.ToDto()).ToArray());
    }

    public async Task<DashboardOverviewDto> GetDashboardOverviewAsync(CancellationToken cancellationToken)
    {
        var nowLocal = dateTimeProvider.UtcNow.ToLocalTime();
        var today = nowLocal.Date;
        var campaigns = await unitOfWork.GetCampaignsAsync(cancellationToken);
        var devices = await unitOfWork.GetDevicesAsync(cancellationToken);
        var responses = await unitOfWork.GetResponsesAsync(cancellationToken);
        var deliveryLogs = await unitOfWork.GetRecentDeliveryLogsAsync(8, cancellationToken);

        var operationalCampaigns = campaigns.Where(item => item.DeletedAtUtc is null).ToArray();
        var activeCampaigns = operationalCampaigns.Count(item => item.Status == CampaignStatus.Active);
        var inactiveCampaigns = operationalCampaigns.Count(item => item.Status != CampaignStatus.Active);
        var recentResponses = responses
            .Where(item => item.AnsweredAtUtc.ToLocalTime() >= nowLocal.AddDays(-6))
            .ToArray();
        var numericResponses = recentResponses
            .Where(item => item.QuestionType == CampaignQuestionType.Scale && item.NumericValue.HasValue)
            .ToArray();
        var textResponses = recentResponses
            .Where(item => item.QuestionType == CampaignQuestionType.Text && !string.IsNullOrWhiteSpace(item.TextValue))
            .ToArray();
        var yesNoResponses = recentResponses
            .Where(item =>
                item.QuestionType == CampaignQuestionType.YesNo &&
                !string.IsNullOrWhiteSpace(item.TextValue))
            .ToArray();
        var choiceResponses = recentResponses
            .Where(item =>
                item.QuestionType == CampaignQuestionType.Choice &&
                !string.IsNullOrWhiteSpace(item.TextValue))
            .ToArray();
        var hasSignal = numericResponses.Length > 0;
        var averageMood = hasSignal ? Math.Round(numericResponses.Average(item => item.NumericValue!.Value), 2) : (double?)null;

        var respondedDevices = recentResponses
            .Select(item => item.DeviceId)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var participationRate = devices.Count == 0
            ? (int?)null
            : Math.Min(100, (int)Math.Round((double)respondedDevices / devices.Count * 100));

        var offlineDevices = devices.Count(item =>
        {
            var lastSeenLocal = item.LastSeenAtUtc.ToLocalTime();
            return nowLocal - lastSeenLocal > TimeSpan.FromMinutes(30);
        });

        var responsesToday = recentResponses
            .Where(item => item.AnsweredAtUtc.ToLocalTime().Date == today)
            .Select(GetSubmissionKey)
            .Distinct()
            .Count();

        var pulseTrend = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var day = today.AddDays(-6 + offset);
                var dayResponses = numericResponses
                    .Where(item => item.AnsweredAtUtc.ToLocalTime().Date == day)
                    .ToArray();

                return new DashboardPulseTrendPointDto(
                    day.ToString("ddd"),
                    dayResponses.Length == 0 ? null : Math.Round(dayResponses.Average(item => item.NumericValue!.Value), 1));
            })
            .ToArray();

        var scaleDistribution = numericResponses
            .GroupBy(item => item.NumericValue!.Value)
            .OrderBy(group => group.Key)
            .Select(group => new DashboardScaleDistributionBucketDto(
                group.Key.ToString(),
                group.Count(),
                Math.Round(group.Count() * 100d / numericResponses.Length, 1)))
            .ToArray();

        var distribution = numericResponses.Length == 0
            ? Array.Empty<DashboardDistributionBucketDto>()
            : Enumerable.Range(numericResponses.Min(item => item.NumericValue!.Value), numericResponses.Max(item => item.NumericValue!.Value) - numericResponses.Min(item => item.NumericValue!.Value) + 1)
            .Select(value => new DashboardDistributionBucketDto(
                value.ToString(),
                numericResponses.Count(item => item.NumericValue == value)))
            .ToArray();

        var totalTypedResponses = numericResponses.Length + textResponses.Length + yesNoResponses.Length + choiceResponses.Length;
        var responseMix = new[]
        {
            new DashboardResponseMixBucketDto(
                "Escala numerica",
                numericResponses.Length,
                totalTypedResponses == 0 ? 0 : Math.Round(numericResponses.Length * 100d / totalTypedResponses, 1)),
            new DashboardResponseMixBucketDto(
                "Texto",
                textResponses.Length,
                totalTypedResponses == 0 ? 0 : Math.Round(textResponses.Length * 100d / totalTypedResponses, 1)),
            new DashboardResponseMixBucketDto(
                "Si/No",
                yesNoResponses.Length,
                totalTypedResponses == 0 ? 0 : Math.Round(yesNoResponses.Length * 100d / totalTypedResponses, 1)),
            new DashboardResponseMixBucketDto(
                "Personalizada",
                choiceResponses.Length,
                totalTypedResponses == 0 ? 0 : Math.Round(choiceResponses.Length * 100d / totalTypedResponses, 1)),
        };

        var noResponseCount = Math.Max(0, devices.Count - respondedDevices);

        var pulseDelta = CalculatePulseDelta(numericResponses, today);

        var alerts = BuildOverviewAlerts(activeCampaigns, inactiveCampaigns, hasSignal, averageMood, participationRate, offlineDevices);
        var pendingAlerts = alerts.Count(item => item.Tone != "positive");

        var healthTone = ResolveHealthTone(activeCampaigns, hasSignal, averageMood, participationRate, offlineDevices);
        var healthLabel = healthTone switch
        {
            "healthy" => "Sistema saludable",
            "attention" => "Sistema en atencion",
            "risk" => "Sistema en riesgo",
            _ when activeCampaigns == 0 => "Sin campanas activas",
            _ => "Sin señal suficiente"
        };

        var metrics = new[]
        {
            new DashboardMetricDto(
                "Campañas activas",
                activeCampaigns.ToString(),
                inactiveCampaigns > 0 ? $"{inactiveCampaigns} fuera de ejecucion" : "sin riesgo operativo",
                inactiveCampaigns > 0 ? "warning" : "up"),
            new DashboardMetricDto(
                "Dispositivos",
                devices.Count.ToString(),
                offlineDevices > 0 ? $"{offlineDevices} fuera de linea" : "100% online",
                offlineDevices > 0 ? "warning" : "up"),
            new DashboardMetricDto(
                "Respuestas hoy",
                responsesToday.ToString(),
                responsesToday == 0 ? "sin respuestas hoy" : "lectura activa",
                responsesToday == 0 ? "neutral" : "up"),
            new DashboardMetricDto(
                "Pulso promedio",
                averageMood?.ToString("0.0") ?? "-",
                pulseDelta ?? (hasSignal ? "sin comparativa diaria" : "sin respuestas suficientes"),
                pulseDelta is null ? "neutral" : pulseDelta.StartsWith('-') ? "down" : "up")
        };

        var actions = operationalCampaigns
            .OrderBy(item => item.Status == CampaignStatus.Active ? 0 : item.Status == CampaignStatus.Paused ? 1 : 2)
            .ThenByDescending(item => item.UpdatedAtUtc)
            .Take(3)
            .Select(item => new DashboardActionItemDto(
                item.Id,
                item.Name,
                DescribeRule(item.ScheduleRule),
                item.Status.ToString(),
                item.Status == CampaignStatus.Draft ? "Editar" : item.Status == CampaignStatus.Paused ? "Reactivar" : "Ver detalle"))
            .ToArray();

        var recentActivity = BuildRecentActivity(deliveryLogs, recentResponses, campaigns);
        var latestEvent = recentActivity.FirstOrDefault() ?? "Sin actividad critica en los ultimos minutos.";
        var insight = BuildOverviewInsight(hasSignal, averageMood, participationRate, distribution);

        return new DashboardOverviewDto(
            healthTone,
            healthLabel,
            hasSignal,
            activeCampaigns,
            devices.Count,
            responsesToday,
            averageMood,
            pulseDelta,
            participationRate,
            pendingAlerts,
            latestEvent,
            alerts,
            metrics,
            pulseTrend,
            responseMix,
            scaleDistribution,
            noResponseCount,
            actions,
            recentActivity,
            insight);
    }

    public async Task<ReportExportData> GetReportExportDataAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        Guid? campaignId,
        string? campaignSearch,
        string? operation,
        CancellationToken cancellationToken)
    {
        if (fromDate.HasValue && toDate.HasValue && toDate.Value < fromDate.Value)
        {
            throw new ArgumentException("toDate must be greater than or equal to fromDate.");
        }

        var campaigns = await unitOfWork.GetCampaignsAsync(cancellationToken);
        var devices = await unitOfWork.GetDevicesAsync(cancellationToken);
        var responses = await unitOfWork.GetResponsesAsync(cancellationToken);
        var campaignLookup = campaigns.ToDictionary(item => item.Id);
        var normalizedCampaignSearch = campaignSearch?.Trim();
        var normalizedOperation = operation?.Trim();
        var filteredCampaigns = campaigns
            .Where(item => !campaignId.HasValue || item.Id == campaignId.Value)
            .Where(item => string.IsNullOrWhiteSpace(normalizedCampaignSearch) ||
                item.Name.Contains(normalizedCampaignSearch, StringComparison.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrWhiteSpace(normalizedOperation) ||
                CampaignMatchesOperation(item, normalizedOperation))
            .Where(item =>
            {
                if (!fromDate.HasValue || !toDate.HasValue)
                {
                    return true;
                }

                var campaignCreatedDate = DateOnly.FromDateTime(item.CreatedAtUtc.ToLocalTime().DateTime);
                return campaignCreatedDate >= fromDate.Value && campaignCreatedDate <= toDate.Value;
            })
            .ToArray();
        var filteredCampaignIds = filteredCampaigns
            .Select(item => item.Id)
            .ToHashSet();

        var filteredResponses = responses
            .Where(item => filteredCampaignIds.Contains(item.CampaignId))
            .Where(item => string.IsNullOrWhiteSpace(normalizedOperation) ||
                item.Operation.Equals(normalizedOperation, StringComparison.OrdinalIgnoreCase) ||
                (campaignLookup.TryGetValue(item.CampaignId, out var campaign) && CampaignMatchesOperation(campaign, normalizedOperation)))
            .OrderByDescending(item => item.AnsweredAtUtc)
            .ToArray();
        var effectiveFromDate = fromDate ??
            (filteredResponses.Length == 0
                ? DateOnly.FromDateTime(dateTimeProvider.UtcNow.ToLocalTime().Date)
                : filteredResponses.Min(item => DateOnly.FromDateTime(item.AnsweredAtUtc.ToLocalTime().DateTime)));
        var effectiveToDate = toDate ??
            (filteredResponses.Length == 0
                ? effectiveFromDate
                : filteredResponses.Max(item => DateOnly.FromDateTime(item.AnsweredAtUtc.ToLocalTime().DateTime)));
        var rangeLength = effectiveToDate.DayNumber - effectiveFromDate.DayNumber + 1;

        var totalSubmissions = filteredResponses
            .Select(GetSubmissionKey)
            .Distinct()
            .Count();

        var uniqueUsers = filteredResponses
            .Select(item => string.IsNullOrWhiteSpace(item.Email) ? item.UserId : item.Email)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var numericResponses = filteredResponses
            .Where(item => item.QuestionType == CampaignQuestionType.Scale && item.NumericValue.HasValue)
            .ToArray();

        var dailyMetrics = Enumerable.Range(0, rangeLength)
            .Select(offset =>
            {
                var date = effectiveFromDate.AddDays(offset);
                var dayResponses = filteredResponses
                    .Where(item => DateOnly.FromDateTime(item.AnsweredAtUtc.ToLocalTime().DateTime) == date)
                    .ToArray();
                var dayNumeric = dayResponses
                    .Where(item => item.QuestionType == CampaignQuestionType.Scale && item.NumericValue.HasValue)
                    .ToArray();
                var dayText = dayResponses
                    .Count(item =>
                        (item.QuestionType == CampaignQuestionType.Text ||
                         item.QuestionType == CampaignQuestionType.Choice ||
                         item.QuestionType == CampaignQuestionType.YesNo) &&
                        !string.IsNullOrWhiteSpace(item.TextValue));

                return new ReportDailyMetric(
                    date,
                    dayResponses.Length,
                    dayResponses.Select(GetSubmissionKey).Distinct().Count(),
                    dayNumeric.Length,
                    dayText,
                    dayNumeric.Length == 0 ? 0 : Math.Round(dayNumeric.Average(item => item.NumericValue!.Value), 2));
            })
            .ToArray();

        var campaignMetrics = filteredCampaigns
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(campaign =>
            {
                var campaignResponses = filteredResponses.Where(item => item.CampaignId == campaign.Id).ToArray();
                var campaignNumeric = campaignResponses
                    .Where(item => item.QuestionType == CampaignQuestionType.Scale && item.NumericValue.HasValue)
                    .ToArray();

                return new ReportCampaignMetric(
                    campaign.Name,
                    campaign.Audience,
                    GetCampaignReportStatus(campaign),
                    campaignResponses.Length,
                    campaignResponses.Select(GetSubmissionKey).Distinct().Count(),
                    campaignNumeric.Length == 0 ? 0 : Math.Round(campaignNumeric.Average(item => item.NumericValue!.Value), 2),
                    campaign.UpdatedAtUtc);
            })
            .ToArray();

        var employeeReportFieldsLookup = await BuildEmployeeReportFieldsLookupAsync(filteredResponses, cancellationToken);

        var responseRows = filteredResponses
            .Select(response =>
            {
                var campaign = campaignLookup.TryGetValue(response.CampaignId, out var value) ? value : null;
                var (minValue, maxValue) = GetQuestionBounds(response, campaignLookup);
                employeeReportFieldsLookup.TryGetValue(response.EmployeeId.Trim(), out var employeeReportFields);

                return new ReportResponseRow(
                    campaign?.Name ?? "Campaña desconocida",
                    campaign?.Audience ?? "-",
                    response.QuestionText,
                    response.QuestionType,
                    response.NumericValue,
                    minValue,
                    maxValue,
                    response.TextValue,
                    response.UserName,
                    response.Email,
                    response.EmployeeId,
                    employeeReportFields?.PayrollCompany ?? string.Empty,
                    employeeReportFields?.Country ?? string.Empty,
                    employeeReportFields?.InternalEmployeeCategory ?? string.Empty,
                    employeeReportFields?.JobTitle ?? string.Empty,
                    response.EntraObjectId,
                    response.UserPrincipalName,
                    response.Operation,
                    response.EmployeeStatus,
                    response.LeaderSolvoId,
                    response.LeaderFullName,
                    response.LeaderCorporateEmail,
                    response.Department,
                    response.Hostname,
                    response.DeviceId,
                    GetSubmissionKey(response),
                    response.AnsweredAtUtc.ToLocalTime());
            })
            .ToArray();

        return new ReportExportData(
            effectiveFromDate,
            effectiveToDate,
            dateTimeProvider.UtcNow,
            filteredCampaigns.Count(item => item.DeletedAtUtc is null && item.Status == CampaignStatus.Active),
            devices.Count,
            filteredResponses.Length,
            totalSubmissions,
            uniqueUsers,
            numericResponses.Length == 0 ? 0 : Math.Round(numericResponses.Average(item => item.NumericValue!.Value), 2),
            dailyMetrics,
            campaignMetrics,
            responseRows);
    }

    private async Task<Dictionary<string, EmployeeReportFields>> BuildEmployeeReportFieldsLookupAsync(
        IReadOnlyCollection<PulseResponse> responses,
        CancellationToken cancellationToken)
    {
        var employeeIds = responses
            .Select(item => item.EmployeeId.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new Dictionary<string, EmployeeReportFields>(
            await employeeOperationsProfileResolver.ResolveReportFieldsAsync(employeeIds, cancellationToken),
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Device> UpsertDeviceAsync(RegisterDeviceRequest request, CancellationToken cancellationToken)
    {
        var device = await unitOfWork.GetDeviceByDeviceIdAsync(request.DeviceId, cancellationToken);
        if (device is null)
        {
            device = new Device
            {
                Id = Guid.NewGuid(),
                DeviceId = request.DeviceId,
                FirstSeenAtUtc = dateTimeProvider.UtcNow
            };
            await unitOfWork.AddDeviceAsync(device, cancellationToken);
        }

        await ApplyDeviceIdentityAsync(device, request, cancellationToken);
        device.OperatingSystem = request.OperatingSystem.Trim();
        device.AgentVersion = request.AgentVersion.Trim();
        device.LastSeenAtUtc = dateTimeProvider.UtcNow;
        return device;
    }

    private async Task ApplyDeviceIdentityAsync(Device device, RegisterDeviceRequest request, CancellationToken cancellationToken)
    {
        var resolvedIdentity = await employeeIdentityResolver.ResolveAsync(request, cancellationToken);

        device.Hostname = request.Hostname.Trim();
        device.UserId = request.UserId.Trim();
        device.UserName = ResolveValue(resolvedIdentity?.DisplayName, request.UserName);
        device.Email = ResolveValue(resolvedIdentity?.Mail, request.Email);
        device.EmployeeId = ResolveValue(resolvedIdentity?.EmployeeId, device.EmployeeId);
        device.EntraObjectId = ResolveValue(resolvedIdentity?.EntraObjectId, device.EntraObjectId);
        device.UserPrincipalName = ResolveValue(resolvedIdentity?.UserPrincipalName, device.UserPrincipalName);
        device.Department = ResolveValue(resolvedIdentity?.Department, request.Department);

        var operationsProfile = await employeeOperationsProfileResolver.ResolveAsync(device.EmployeeId, cancellationToken);
        if (operationsProfile is null)
        {
            return;
        }

        device.Operation = ResolveValue(operationsProfile.Operation, device.Operation);
        device.EmployeeStatus = ResolveValue(operationsProfile.Status, device.EmployeeStatus);
        device.LeaderSolvoId = ResolveValue(operationsProfile.LeaderSolvoId, device.LeaderSolvoId);
        device.LeaderFullName = ResolveValue(operationsProfile.LeaderFullName, device.LeaderFullName);
        device.LeaderCorporateEmail = ResolveValue(operationsProfile.LeaderCorporateEmail, device.LeaderCorporateEmail);
        device.Client = ResolveValue(operationsProfile.Client, device.Client);
        device.Department = ResolveValue(operationsProfile.Department, device.Department);
    }

    private async Task<Device> EnsureDeviceProfileForActivityAsync(
        AgentActivityEventRequest request,
        CancellationToken cancellationToken)
    {
        var device = await unitOfWork.GetDeviceByDeviceIdAsync(request.DeviceId.Trim(), cancellationToken);
        if (device is null)
        {
            device = new Device
            {
                Id = Guid.NewGuid(),
                DeviceId = request.DeviceId.Trim(),
                FirstSeenAtUtc = dateTimeProvider.UtcNow,
                LastSeenAtUtc = dateTimeProvider.UtcNow
            };
            await unitOfWork.AddDeviceAsync(device, cancellationToken);
        }

        await ApplyDeviceIdentityAsync(
            device,
            new RegisterDeviceRequest(
                request.DeviceId,
                request.Hostname,
                request.UserId,
                request.UserName,
                request.Email,
                request.Department,
                device.OperatingSystem,
                device.AgentVersion),
            cancellationToken);

        device.LastSeenAtUtc = dateTimeProvider.UtcNow;
        return device;
    }

    private async Task SendLockedDeviceLeaderAlertIfNeededAsync(
        Device? device,
        AgentActivityEvent? activityEvent,
        AgentActivityEventRequest request,
        CancellationToken cancellationToken)
    {
        if (device is null || !IsLockedAlertCandidate(request.EventType))
        {
            return;
        }

        var client = NormalizeClientToken(device.Client);
        var operation = NormalizeOperationToken(device.Operation);
        if (client.Length == 0 && operation.Length == 0)
        {
            return;
        }

        var settings = await unitOfWork.GetEnabledClientInactivityAlertSettingsForScopeAsync(client, operation, cancellationToken);
        if (settings.Count == 0)
        {
            return;
        }

        var lockedSeconds = Math.Max(0, request.DurationSeconds ?? activityEvent?.DurationSeconds ?? 0);
        if (lockedSeconds <= 0)
        {
            return;
        }

        var lockedAtUtc = request.LockedAtUtc ?? request.OccurredAtUtc.AddSeconds(-lockedSeconds);
        lockedAtUtc = TruncateToSecond(lockedAtUtc);
        var lockedAtLocal = request.LockedAtLocal ?? lockedAtUtc.ToLocalTime();
        var alertedAtLocal = request.OccurredAtLocal ?? request.OccurredAtUtc.ToLocalTime();

        foreach (var setting in settings)
        {
            var additionalRecipientEmails = DeserializeAdditionalRecipientEmails(setting.AdditionalRecipientEmailsJson);
            if (string.IsNullOrWhiteSpace(device.LeaderCorporateEmail) && additionalRecipientEmails.Count == 0)
            {
                continue;
            }

            var thresholdMinutes = Math.Clamp(setting.AlertThresholdMinutes, MinClientAlertThresholdMinutes, MaxClientAlertThresholdMinutes);
            if (lockedSeconds < thresholdMinutes * 60)
            {
                continue;
            }

            var alreadySent = await unitOfWork.LockedSessionAlertNotificationExistsAsync(
                device.DeviceId,
                lockedAtUtc,
                thresholdMinutes,
                cancellationToken);
            if (alreadySent)
            {
                continue;
            }

            await leaderAlertEmailService.SendLockedDeviceAlertAsync(
                new LeaderLockAlertEmailRequest(
                    device.LeaderCorporateEmail,
                    additionalRecipientEmails,
                    device.LeaderFullName,
                    device.UserName,
                    device.Email,
                    device.EmployeeId,
                    device.Operation,
                    device.EmployeeStatus,
                    device.Department,
                    request.LockReason ?? activityEvent?.LockReason ?? string.Empty,
                    request.IdleSecondsAtLock ?? activityEvent?.IdleSecondsAtLock,
                    thresholdMinutes,
                    lockedAtUtc,
                    request.OccurredAtUtc,
                    lockedAtLocal,
                    alertedAtLocal),
                cancellationToken);

            await unitOfWork.AddLockedSessionAlertNotificationAsync(
                new LockedSessionAlertNotification
                {
                    Id = Guid.NewGuid(),
                    DeviceId = device.DeviceId,
                    EmployeeId = device.EmployeeId,
                    Client = client,
                    LockedAtUtc = lockedAtUtc,
                    ThresholdMinutes = thresholdMinutes,
                    SentAtUtc = dateTimeProvider.UtcNow
                },
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private static string ResolveValue(string? preferred, string? fallback)
        => !string.IsNullOrWhiteSpace(preferred)
            ? preferred.Trim()
            : fallback?.Trim() ?? string.Empty;

    private static IReadOnlyList<string> NormalizeAdditionalRecipientEmails(IReadOnlyList<string>? emails)
    {
        if (emails is null || emails.Count == 0)
        {
            return [];
        }

        var normalizedEmails = emails
            .SelectMany(item => item.Split([';', ',', '\n', '\r', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var email in normalizedEmails)
        {
            if (!IsValidEmail(email))
            {
                throw new InvalidOperationException($"El correo adicional '{email}' no es valido.");
            }
        }

        return normalizedEmails;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var address = new MailAddress(email);
            return string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string SerializeAdditionalRecipientEmails(IReadOnlyList<string> emails)
        => emails.Count == 0 ? "[]" : JsonSerializer.Serialize(emails);

    private static IReadOnlyList<string> DeserializeAdditionalRecipientEmails(string? emailsJson)
    {
        if (string.IsNullOrWhiteSpace(emailsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(emailsJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool IsLockedAlertCandidate(string? eventType)
        => string.Equals(eventType?.Trim(), "SessionLockedThresholdReached", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(eventType?.Trim(), "SessionLockedDurationObserved", StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset TruncateToSecond(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
    }

    private async Task<HashSet<string>> ResolveKnownOperationsAsync(CancellationToken cancellationToken)
    {
        var devices = await unitOfWork.GetDevicesAsync(cancellationToken);
        return devices
            .Select(item => NormalizeOperationToken(item.Operation))
            .Where(item => item.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool CampaignMatchesDeviceOperation(Campaign campaign, string? deviceOperation, HashSet<string> knownOperations)
    {
        var selectedOperations = ParseAudienceOperations(campaign.Audience);
        if (selectedOperations.Length == 0)
        {
            return true;
        }

        var deviceOperationToken = NormalizeOperationToken(deviceOperation);
        if (deviceOperationToken.Length == 0)
        {
            return false;
        }

        var selectedOperationTokens = selectedOperations
            .Select(NormalizeOperationToken)
            .Where(item => item.Length > 0)
            .ToArray();

        if (selectedOperationTokens.Length == 0)
        {
            return true;
        }

        var targetsKnownOperation = selectedOperationTokens.Any(knownOperations.Contains);
        if (!targetsKnownOperation)
        {
            return true;
        }

        return selectedOperationTokens.Contains(deviceOperationToken, StringComparer.OrdinalIgnoreCase);
    }

    private static DateTimeOffset? ResolveLastSeenAtLocal(
        Device device,
        IReadOnlyDictionary<string, TimeSpan> localOffsetsByDevice)
    {
        var deviceKey = GetDeviceKey(device);
        if (!string.IsNullOrWhiteSpace(deviceKey) && localOffsetsByDevice.TryGetValue(deviceKey, out var offset))
        {
            return device.LastSeenAtUtc.ToOffset(offset);
        }

        var hostnameKey = device.Hostname.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(hostnameKey) && localOffsetsByDevice.TryGetValue(hostnameKey, out offset))
        {
            return device.LastSeenAtUtc.ToOffset(offset);
        }

        return null;
    }

    private static string GetDeviceKey(Device device)
        => !string.IsNullOrWhiteSpace(device.DeviceId)
            ? device.DeviceId.Trim().ToLowerInvariant()
            : device.Hostname.Trim().ToLowerInvariant();

    private static string GetActivityDeviceKey(AgentActivityEvent activityEvent)
        => !string.IsNullOrWhiteSpace(activityEvent.DeviceId)
            ? activityEvent.DeviceId.Trim().ToLowerInvariant()
            : activityEvent.Hostname.Trim().ToLowerInvariant();

    private static string NormalizeAudience(string audience)
    {
        var selectedOperations = audience
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => NormalizeRequiredPlainText(item, 120, "Campaign audience"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (selectedOperations.Length == 0 ||
            selectedOperations.Any(IsAllOperationsAudience))
        {
            return AllOperationsAudience;
        }

        var normalizedAudience = string.Join(", ", selectedOperations);
        if (normalizedAudience.Length > 200)
        {
            throw new ArgumentException("Campaign audience must be 200 characters or fewer.");
        }

        return normalizedAudience;
    }

    private static string[] ParseAudienceOperations(string? audience)
    {
        if (string.IsNullOrWhiteSpace(audience) || IsAllOperationsAudience(audience))
        {
            return [];
        }

        return audience
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
    }

    private static bool IsAllOperationsAudience(string audience)
    {
        var normalized = audience.Trim();
        return normalized.Equals(AllOperationsAudience, StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("All operations", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("Todos", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("All", StringComparison.OrdinalIgnoreCase);
    }

    private static bool CampaignMatchesOperation(Campaign campaign, string operation)
        => IsAllOperationsAudience(campaign.Audience) ||
           ParseAudienceOperations(campaign.Audience)
               .Any(item => item.Equals(operation, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeOperationToken(string? operation)
        => string.IsNullOrWhiteSpace(operation)
            ? string.Empty
            : operation.Trim().ToUpperInvariant();

    private static string NormalizeClientToken(string? client)
        => string.IsNullOrWhiteSpace(client)
            ? string.Empty
            : client.Trim();

    private static void AddKnownOperation(HashSet<string> knownOperations, string? operation)
    {
        var normalizedOperation = NormalizeOperationToken(operation);
        if (normalizedOperation.Length > 0)
        {
            knownOperations.Add(normalizedOperation);
        }
    }

    private static string? CalculatePulseDelta(IReadOnlyList<PulseResponse> numericResponses, DateTime today)
    {
        var todayValues = numericResponses
            .Where(item => item.AnsweredAtUtc.ToLocalTime().Date == today && item.NumericValue.HasValue)
            .Select(item => item.NumericValue!.Value)
            .ToArray();
        var yesterdayValues = numericResponses
            .Where(item => item.AnsweredAtUtc.ToLocalTime().Date == today.AddDays(-1) && item.NumericValue.HasValue)
            .Select(item => item.NumericValue!.Value)
            .ToArray();

        if (todayValues.Length == 0 || yesterdayValues.Length == 0)
        {
            return null;
        }

        var delta = Math.Round(todayValues.Average() - yesterdayValues.Average(), 1);
        if (delta == 0)
        {
            return "+0.0";
        }

        return delta > 0 ? $"+{delta:0.0}" : $"{delta:0.0}";
    }

    private static string ResolveHealthTone(
        int activeCampaigns,
        bool hasSignal,
        double? averageMood,
        int? participationRate,
        int offlineDevices)
    {
        if (activeCampaigns == 0 || !hasSignal)
        {
            return "neutral";
        }

        if ((averageMood.HasValue && averageMood.Value < 3.0) || (participationRate.HasValue && participationRate.Value < 35))
        {
            return "risk";
        }

        if ((averageMood.HasValue && averageMood.Value < 3.8) || (participationRate.HasValue && participationRate.Value < 55) || offlineDevices > 0)
        {
            return "attention";
        }

        return "healthy";
    }

    private static IReadOnlyList<DashboardAlertDto> BuildOverviewAlerts(
        int activeCampaigns,
        int inactiveCampaigns,
        bool hasSignal,
        double? averageMood,
        int? participationRate,
        int offlineDevices)
    {
        var alerts = new List<DashboardAlertDto>(3);

        if (activeCampaigns == 0)
        {
            alerts.Add(new DashboardAlertDto(
                "warning",
                "Cobertura detenida",
                "No hay campañas activas.",
                "El sistema no esta enviando mediciones en este momento, asi que no hay lectura nueva del equipo."));
        }
        else if (!hasSignal)
        {
            alerts.Add(new DashboardAlertDto(
                "warning",
                "Sin señal",
                "Aun no hay respuestas registradas.",
                "Todavia no existe base para concluir si el pulso va bien o mal. Primero hay que capturar respuestas."));
        }
        else if (averageMood.HasValue && averageMood.Value < 3.0)
        {
            alerts.Add(new DashboardAlertDto(
                "critical",
                "Riesgo alto",
                $"El pulso promedio cayo a {averageMood.Value:0.0}.",
                "La señal actual refleja desgaste real y requiere intervencion rapida."));
        }
        else
        {
            alerts.Add(new DashboardAlertDto(
                "positive",
                "Señal estable",
                averageMood.HasValue
                    ? $"Pulso promedio en rango operativo ({averageMood.Value:0.0}/5)."
                    : "No hay deterioro visible en la señal principal.",
                "La medicion reciente no muestra caida material del pulso. El foco puede pasar a cobertura y continuidad."));
        }

        if (inactiveCampaigns > 0)
        {
            alerts.Add(new DashboardAlertDto(
                "warning",
                "Cobertura parcial",
                $"{inactiveCampaigns} campañas estan fuera de ejecucion activa.",
                "Hay campañas pausadas o en borrador que reducen cobertura y continuidad."));
        }
        else if (participationRate.HasValue && participationRate.Value < 45)
        {
            alerts.Add(new DashboardAlertDto(
                "warning",
                "Atencion",
                $"Participacion baja ({participationRate.Value}%).",
                "La muestra todavia puede estar sesgada. Conviene reforzar timing o relanzar la medicion."));
        }
        else
        {
            alerts.Add(new DashboardAlertDto(
                "positive",
                "Muestra util",
                participationRate.HasValue ? $"Participacion saludable ({participationRate.Value}%)." : "Cobertura lista para medir.",
                "La muestra actual alcanza para leer tendencia con menos sesgo operativo."));
        }

        if (offlineDevices > 0)
        {
            alerts.Add(new DashboardAlertDto(
                "warning",
                "Seguimiento",
                $"{offlineDevices} dispositivos fuera de linea.",
                "No todos los agentes estan reportando a tiempo. Conviene revisar conectividad."));
        }
        else
        {
            alerts.Add(new DashboardAlertDto(
                "positive",
                "Canal vivo",
                "Dispositivos conectados.",
                "Los agentes siguen reportando y el canal de captura no presenta friccion visible."));
        }

        return alerts;
    }

    private static IReadOnlyList<string> BuildRecentActivity(
        IReadOnlyList<DeliveryLog> deliveryLogs,
        IReadOnlyList<PulseResponse> recentResponses,
        IReadOnlyList<Campaign> campaigns)
    {
        var items = new List<(DateTimeOffset When, string Message)>();

        items.AddRange(deliveryLogs.Select(item => (
            item.PromptedAtUtc,
            $"Campaña {item.Campaign.Name}: {item.Status.ToLowerInvariant()} en {item.Hostname}.")));

        items.AddRange(recentResponses
            .OrderByDescending(item => item.AnsweredAtUtc)
            .Take(3)
            .Select(item => (
                item.AnsweredAtUtc,
                item.NumericValue.HasValue
                    ? $"{item.UserName} respondio {item.NumericValue.Value}/5 en {item.Hostname}."
                    : $"{item.UserName} envio una respuesta abierta en {item.Hostname}.")));

        items.AddRange(campaigns
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Take(2)
            .Select(item => (
                item.UpdatedAtUtc,
                $"Campaña actualizada: {item.Name} ({item.Status}).")));

        return items
            .OrderByDescending(item => item.When)
            .Select(item => item.Message)
            .Distinct()
            .Take(4)
            .ToArray();
    }

    private static DashboardInsightDto BuildOverviewInsight(
        bool hasSignal,
        double? averageMood,
        int? participationRate,
        IReadOnlyList<DashboardDistributionBucketDto> distribution)
    {
        if (!hasSignal)
        {
            return new DashboardInsightDto(
                "attention",
                "Sin base suficiente",
                "Todavia no existe muestra para leer pulso o riesgo.",
                "Antes de interpretar clima o desgaste, el sistema necesita respuestas reales de una campaña activa.");
        }

        var dominantResponse = distribution
            .Where(item => item.Label != "Sin respuesta")
            .OrderByDescending(item => item.Value)
            .FirstOrDefault();

        var dominantMeaning = dominantResponse?.Label switch
        {
            "1" => "critico",
            "2" => "bajo",
            "3" => "neutral",
            "4" => "positivo",
            "5" => "muy positivo",
            _ => "neutral"
        };

        if (averageMood.HasValue && averageMood.Value < 3.5 || participationRate.HasValue && participationRate.Value < 45)
        {
            return new DashboardInsightDto(
                "attention",
                "Lectura operativa",
                $"La respuesta dominante esta en {dominantResponse?.Label ?? "3"} · {dominantMeaning}.",
                "Hay señales de desgaste o una base demasiado corta para confiar en estabilidad. Conviene reforzar participacion o lanzar una medicion correctiva.");
        }

        return new DashboardInsightDto(
            "positive",
            "Lectura operativa",
            $"La respuesta dominante esta en {dominantResponse?.Label ?? "3"} · {dominantMeaning}.",
            dominantResponse?.Label is "3"
                ? "La organizacion responde en zona neutra: no hay crisis, pero tampoco una señal fuerte de compromiso. Lo siguiente es mover esa neutralidad."
                : "La señal es favorable y consistente. El siguiente foco es sostener el ritmo y revisar segmentos con menor cobertura.");
    }

    private static string DescribeRule(string scheduleRule)
    {
        if (string.IsNullOrWhiteSpace(scheduleRule))
        {
            return "Sin programación definida.";
        }

        var trimmed = scheduleRule.Trim();
        var sections = trimmed.Split('#', 2, StringSplitOptions.TrimEntries);
        var cron = sections[0];
        var flags = sections.Length > 1
            ? sections[1]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(item => item.ToLowerInvariant())
                .ToArray()
            : Array.Empty<string>();

        var parts = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 6)
        {
            return BuildRuleDetail("Programación personalizada.", flags);
        }

        var frequency = flags
            .FirstOrDefault(item => item.StartsWith("freq=", StringComparison.OrdinalIgnoreCase))?
            .Split('=', 2)[1];
        string baseDescription;
        if (string.Equals(frequency, "immediate", StringComparison.OrdinalIgnoreCase) ||
            (parts[1] == "*" && parts[2] == "*"))
        {
            baseDescription = "Se envía de inmediato.";
        }
        else if (string.Equals(frequency, "hourly", StringComparison.OrdinalIgnoreCase) || parts[2] == "*")
        {
            baseDescription = $"Se envía cada hora al minuto {parts[1].PadLeft(2, '0')}{DescribeDays(parts[5])}.";
        }
        else
        {
            var hour = int.TryParse(parts[2], out var parsedHour) ? parsedHour : 0;
            var minute = int.TryParse(parts[1], out var parsedMinute) ? parsedMinute : 0;
            var localTime = new TimeOnly(hour, minute).ToString("h:mm tt", CultureInfo.InvariantCulture);
            baseDescription = frequency?.ToLowerInvariant() switch
            {
                "weekly" => $"Se envía cada semana a las {localTime}{DescribeDays(parts[5])}.",
                "biweekly" => $"Se envía cada dos semanas a las {localTime}{DescribeDays(parts[5])}.",
                "monthly" => $"Se envía cada mes a las {localTime}{DescribeDays(parts[5])}.",
                "quarterly" => $"Se envía cada trimestre a las {localTime}{DescribeDays(parts[5])}.",
                _ => $"Se envía a las {localTime}{DescribeDays(parts[5])}."
            };
        }

        return BuildRuleDetail(baseDescription, flags);
    }

    private static string BuildRuleDetail(string baseDescription, IReadOnlyCollection<string> flags)
    {
        var notes = new List<string>(2);
        if (flags.Contains("force-response") || flags.Contains("no-dismiss"))
        {
            notes.Add("Requiere respuesta");
        }

        if (notes.Count == 0)
        {
            return baseDescription;
        }

        return $"{baseDescription} {string.Join(". ", notes)}.";
    }

    private static string DescribeDays(string dayToken)
    {
        if (string.IsNullOrWhiteSpace(dayToken) || dayToken == "*")
        {
            return string.Empty;
        }

        var normalized = dayToken.Trim().ToUpperInvariant();
        return normalized switch
        {
            "MON-FRI" => " de lunes a viernes",
            "SAT,SUN" => " los fines de semana",
            _ => $" {TranslateDayList(normalized)}"
        };
    }

    private static string TranslateDayList(string rawDays)
    {
        var dayMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MON"] = "lunes",
            ["TUE"] = "martes",
            ["WED"] = "miércoles",
            ["THU"] = "jueves",
            ["FRI"] = "viernes",
            ["SAT"] = "sábado",
            ["SUN"] = "domingo"
        };

        var translatedDays = rawDays
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => dayMap.TryGetValue(item, out var translated) ? translated : item.ToLowerInvariant())
            .ToArray();

        return translatedDays.Length switch
        {
            0 => string.Empty,
            1 => $"los {translatedDays[0]}",
            2 => $"los {translatedDays[0]} y {translatedDays[1]}",
            _ => $"los {string.Join(", ", translatedDays.Take(translatedDays.Length - 1))} y {translatedDays[^1]}"
        };
    }

    private static string FormatAnswerForLiveEvent(PulseResponseDto response)
    {
        if (response.QuestionType == CampaignQuestionType.Text ||
            response.QuestionType == CampaignQuestionType.Choice ||
            response.QuestionType == CampaignQuestionType.YesNo)
        {
            return string.IsNullOrWhiteSpace(response.TextValue) ? "texto" : response.TextValue;
        }

        return response.NumericValue is int value ? $"{value}" : "sin valor";
    }

    private static string FormatActivityReason(string? lockReason, int? idleSecondsAtLock)
    {
        if (string.Equals(lockReason, "AutoLock", StringComparison.OrdinalIgnoreCase))
        {
            return idleSecondsAtLock is int idleSeconds
                ? $"bloqueo por inactividad tras {FormatDuration(idleSeconds)}"
                : "bloqueo por inactividad";
        }

        if (string.Equals(lockReason, "ManualLock", StringComparison.OrdinalIgnoreCase))
        {
            return "bloqueo manual";
        }

        return "bloqueo detectado";
    }

    private static string FormatDuration(int totalSeconds)
    {
        if (totalSeconds < 0)
        {
            totalSeconds = 0;
        }

        var duration = TimeSpan.FromSeconds(totalSeconds);
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours} h {duration.Minutes} min {duration.Seconds} s";
        }

        if (duration.TotalMinutes >= 1)
        {
            return $"{duration.Minutes} min {duration.Seconds} s";
        }

        return $"{duration.Seconds} s";
    }

    private static string FormatSuspendContext(int? idleSecondsBeforeSuspend)
    {
        return idleSecondsBeforeSuspend is int idleSeconds
            ? $"inactividad previa de {FormatDuration(idleSeconds)}"
            : "suspension detectada";
    }

    private static Guid GetSubmissionKey(PulseResponse response)
        => response.SubmissionId == Guid.Empty ? response.Id : response.SubmissionId;

    private static (int? MinValue, int? MaxValue) GetQuestionBounds(
        PulseResponse response,
        IReadOnlyDictionary<Guid, Campaign> campaignLookup)
    {
        if (response.QuestionType != CampaignQuestionType.Scale ||
            !campaignLookup.TryGetValue(response.CampaignId, out var campaign))
        {
            return (null, null);
        }

        var questions = DeserializeQuestions(campaign);
        var question = questions.FirstOrDefault(item => item.Id == response.QuestionId);
        if (question is null && questions.Count == 1)
        {
            question = questions[0];
        }

        return (question?.MinValue ?? campaign.MinValue, question?.MaxValue ?? campaign.MaxValue);
    }

    private static IReadOnlyList<CampaignQuestionData> ResolveQuestionsForRequest(
        IReadOnlyList<CampaignQuestionRequest>? questions,
        string? legacyQuestionText)
    {
        if (questions is { Count: > 0 })
        {
            var normalizedQuestions = questions
                .Where(item => !string.IsNullOrWhiteSpace(item.Text))
                .Select(NormalizeQuestion)
                .ToArray();

            if (normalizedQuestions.Length > 0)
            {
                return normalizedQuestions;
            }
        }

        if (!string.IsNullOrWhiteSpace(legacyQuestionText))
        {
            return
            [
                new CampaignQuestionData(
                    Guid.NewGuid(),
                    NormalizeRequiredPlainText(legacyQuestionText, 500, "Campaign question"),
                    CampaignQuestionType.Scale,
                    1,
                    5,
                    null,
                    null)
            ];
        }

        throw new InvalidOperationException("Campaign must include at least one question.");
    }

    private static string NormalizeRequiredPlainText(string? value, int maxLength, string fieldName)
    {
        if (PlainTextSecurity.TryNormalize(value, maxLength, out var normalized))
        {
            return normalized;
        }

        throw new ArgumentException($"{fieldName} must be plain text and cannot include HTML or script content.", fieldName);
    }

    private static string NormalizePlainText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (PlainTextSecurity.TryNormalize(value, maxLength, out var normalized))
        {
            return normalized;
        }

        throw new ArgumentException("Value must be plain text and cannot include HTML or script content.");
    }

    private static string SerializeQuestions(IReadOnlyList<CampaignQuestionData> questions)
        => JsonSerializer.Serialize(questions, QuestionSerializerOptions);

    private static IReadOnlyList<CampaignQuestionData> DeserializeQuestions(Campaign campaign)
    {
        if (!string.IsNullOrWhiteSpace(campaign.QuestionsJson))
        {
            var parsed = JsonSerializer.Deserialize<List<CampaignQuestionData>>(campaign.QuestionsJson, QuestionSerializerOptions);
            if (parsed is { Count: > 0 })
            {
                return parsed;
            }
        }

        if (!string.IsNullOrWhiteSpace(campaign.QuestionText))
        {
            return
            [
                new CampaignQuestionData(
                    BuildLegacyQuestionId(campaign.Id),
                    campaign.QuestionText,
                    CampaignQuestionType.Scale,
                    campaign.MinValue,
                    campaign.MaxValue,
                    null,
                    null)
            ];
        }

        return [];
    }

    private static Guid BuildLegacyQuestionId(Guid campaignId)
    {
        var bytes = campaignId.ToByteArray();
        bytes[0] ^= 0x52;
        bytes[8] ^= 0x21;
        return new Guid(bytes);
    }

    private static CampaignQuestionData NormalizeQuestion(CampaignQuestionRequest question)
    {
        var normalizedType = question.Type;
        int? minValue = normalizedType == CampaignQuestionType.Scale ? Math.Max(0, question.MinValue ?? 1) : null;
        int? maxValue = normalizedType == CampaignQuestionType.Scale
            ? Math.Max(minValue ?? 1, question.MaxValue ?? 5)
            : null;
        var text = NormalizeRequiredPlainText(question.Text, 500, "Campaign question");
        var placeholder = normalizedType == CampaignQuestionType.Text && !string.IsNullOrWhiteSpace(question.Placeholder)
            ? NormalizeRequiredPlainText(question.Placeholder, 200, "Campaign question placeholder")
            : null;
        var options = normalizedType == CampaignQuestionType.Choice
            ? NormalizeChoiceOptions(question.Options)
            : null;

        return new CampaignQuestionData(
            question.Id is { } questionId && questionId != Guid.Empty ? questionId : Guid.NewGuid(),
            text,
            normalizedType,
            minValue,
            maxValue,
            placeholder,
            options);
    }

    private static IReadOnlyList<string> NormalizeChoiceOptions(IReadOnlyList<string>? options)
    {
        var normalized = (options ?? Array.Empty<string>())
            .Select(item => NormalizePlainText(item, 120))
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();

        if (normalized.Length < 2)
        {
            throw new InvalidOperationException("Las preguntas personalizadas deben tener entre 2 y 5 opciones.");
        }

        return normalized;
    }

    private static string GetCampaignReportStatus(Campaign campaign)
    {
        if (campaign.DeletedAtUtc is not null)
        {
            return "Eliminada";
        }

        return campaign.Status switch
        {
            CampaignStatus.Active => "Activa",
            CampaignStatus.Paused => "Pausada",
            CampaignStatus.Draft => "Borrador",
            _ => campaign.Status.ToString()
        };
    }

    private sealed record CampaignQuestionData(
        Guid Id,
        string Text,
        CampaignQuestionType Type,
        int? MinValue,
        int? MaxValue,
        string? Placeholder,
        IReadOnlyList<string>? Options);
}

internal static class PulseMappings
{
    private static readonly JsonSerializerOptions QuestionSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static CampaignDto ToDto(this Campaign campaign)
    {
        var questions = ResolveQuestions(campaign);
        return new CampaignDto(
            campaign.Id,
            campaign.Name,
            campaign.Audience,
            campaign.ScheduleRule,
            campaign.DeliveryWindowStart,
            campaign.DeliveryWindowEnd,
            campaign.Status,
            questions,
            campaign.CreatedBy,
            campaign.CreatedAtUtc,
            campaign.UpdatedAtUtc,
            campaign.DeletedAtUtc);
    }

    public static PulseResponseDto ToDto(this PulseResponse response, (int? MinValue, int? MaxValue) bounds = default) => new(
        response.Id,
        response.CampaignId,
        response.QuestionId,
        response.QuestionText,
        response.QuestionType,
        response.DeviceId,
        response.UserId,
        response.UserName,
        response.Email,
        response.EmployeeId,
        response.EntraObjectId,
        response.UserPrincipalName,
        response.Operation,
        response.EmployeeStatus,
        response.LeaderSolvoId,
        response.LeaderFullName,
        response.LeaderCorporateEmail,
        response.Department,
        response.Hostname,
        response.NumericValue,
        bounds.MinValue,
        bounds.MaxValue,
        response.TextValue,
        response.SubmissionId,
        response.AnsweredAtUtc);

    public static RegisterDeviceResponse ToRegisterDto(this Device device) => new(device.Id, device.DeviceId, device.FirstSeenAtUtc);

    public static DeviceHeartbeatDto ToHeartbeatDto(this Device device, DateTimeOffset? lastSeenAtLocal = null) => new(
        device.DeviceId,
        device.Hostname,
        device.UserName,
        device.Email,
        device.EmployeeId,
        device.EntraObjectId,
        device.UserPrincipalName,
        device.Operation,
        device.EmployeeStatus,
        device.LeaderSolvoId,
        device.LeaderFullName,
        device.LeaderCorporateEmail,
        device.Client,
        device.Department,
        device.OperatingSystem,
        device.AgentVersion,
        device.LastSeenAtUtc,
        lastSeenAtLocal);

    public static ClientInactivityAlertSettingDto ToDto(this ClientInactivityAlertSetting setting) => new(
        setting.Id,
        setting.Client,
        setting.Operation,
        setting.AlertThresholdMinutes,
        setting.IsEnabled,
        DeserializeAdditionalRecipientEmails(setting.AdditionalRecipientEmailsJson),
        setting.CreatedAtUtc,
        setting.UpdatedAtUtc);

    public static DeliveryLogDto ToDto(this DeliveryLog log) => new(
        log.Id,
        log.CampaignId,
        log.Campaign?.Name ?? string.Empty,
        log.DeviceId,
        log.UserId,
        log.UserName,
        log.Email,
        log.Hostname,
        log.Status,
        log.Error,
        log.RetryCount,
        log.PromptedAtUtc);

    public static AgentActivityEventDto ToDto(this AgentActivityEvent activityEvent) => new(
        activityEvent.Id,
        activityEvent.DeviceId,
        activityEvent.UserId,
        activityEvent.UserName,
        activityEvent.Email,
        activityEvent.Department,
        activityEvent.Hostname,
        activityEvent.EventType,
        activityEvent.LockReason,
        activityEvent.IdleSecondsAtLock,
        activityEvent.DurationSeconds,
        activityEvent.OccurredAtUtc,
        activityEvent.OccurredAtLocal);

    private static IReadOnlyList<string> DeserializeAdditionalRecipientEmails(string? emailsJson)
    {
        if (string.IsNullOrWhiteSpace(emailsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(emailsJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<CampaignQuestionDto> ResolveQuestions(Campaign campaign)
    {
        if (!string.IsNullOrWhiteSpace(campaign.QuestionsJson))
        {
            var parsed = JsonSerializer.Deserialize<List<CampaignQuestionDto>>(campaign.QuestionsJson, QuestionSerializerOptions);
            if (parsed is { Count: > 0 })
            {
                return parsed;
            }
        }

        if (!string.IsNullOrWhiteSpace(campaign.QuestionText))
        {
            return
            [
                new CampaignQuestionDto(
                    BuildLegacyQuestionId(campaign.Id),
                    campaign.QuestionText,
                    CampaignQuestionType.Scale,
                    campaign.MinValue,
                    campaign.MaxValue,
                    null,
                    null)
            ];
        }

        return [];
    }

    private static Guid BuildLegacyQuestionId(Guid campaignId)
    {
        var bytes = campaignId.ToByteArray();
        bytes[0] ^= 0x52;
        bytes[8] ^= 0x21;
        return new Guid(bytes);
    }
}
