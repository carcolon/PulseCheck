using System.Globalization;
using System.Text;
using PulseCheck.Application.Common;
using PulseCheck.Application.Ports;
using PulseCheck.Domain.Abstractions;
using PulseCheck.Domain.Enums;

namespace PulseCheck.Application.Services;

public sealed class TransformationalLeaderDashboardService(
    IPulseCheckUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    IEmployeeOperationsProfileResolver employeeOperationsProfileResolver)
{
    private static readonly TimeZoneInfo DashboardTimeZone = ResolveDashboardTimeZone();

    public async Task<TlDashboardDto> GetDashboardAsync(
        TransformationalLeaderSessionDto session,
        TlDashboardRequest request,
        CancellationToken cancellationToken)
    {
        var campaigns = await unitOfWork.GetCampaignsAsync(cancellationToken);
        var campaignLookup = campaigns.ToDictionary(item => item.Id);
        var selectedWeeks = (request.WeekIds ?? []).Where(item => !string.IsNullOrWhiteSpace(item)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedCampaigns = (request.CampaignIds ?? []).Where(item => item != Guid.Empty).ToHashSet();
        var answerFilters = (request.AnswerFilters ?? [])
            .Where(item => item.QuestionId != Guid.Empty && item.Values is { Count: > 0 })
            .ToDictionary(
                item => item.QuestionId,
                item => item.Values!.Select(NormalizeAnswerValue).Where(value => value.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase));

        var operations = TransformationalLeaderOperationScope.Normalize(session.Operations);
        var operation = TransformationalLeaderOperationScope.Format(operations);
        var includeAllOperations = TransformationalLeaderOperationScope.IncludesAllOperations(operations);
        var scopedCampaigns = campaigns
            .Where(item => includeAllOperations || CampaignMatchesAnyOperation(item, operations))
            .ToArray();
        var scopedCampaignIds = scopedCampaigns.Select(item => item.Id).ToHashSet();
        var responses = (await unitOfWork.GetResponsesAsync(cancellationToken))
            .Where(item => campaignLookup.ContainsKey(item.CampaignId))
            .Where(item => includeAllOperations || scopedCampaignIds.Contains(item.CampaignId))
            .Where(item => includeAllOperations || ResponseIsInOperationScope(item, campaignLookup[item.CampaignId], operations, includeAllOperations))
            .ToArray();

        var weekOptions = BuildLatestWeekOptions(dateTimeProvider.UtcNow, 16);
        var availableWeekIds = weekOptions.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var deliveryLogs = await unitOfWork.GetDeliveryLogsSinceAsync(ToDashboardStartUtc(weekOptions.Min(item => item.StartsAt)), cancellationToken);
        var deliveryLogsByCampaign = deliveryLogs
            .Where(item => scopedCampaignIds.Contains(item.CampaignId))
            .GroupBy(item => item.CampaignId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var responsesByCampaign = responses
            .GroupBy(item => item.CampaignId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var campaignWeekIds = scopedCampaigns.ToDictionary(
            campaign => campaign.Id,
            campaign => ResolveCampaignWeekIds(campaign, responsesByCampaign, deliveryLogsByCampaign, availableWeekIds));

        var campaignOptions = scopedCampaigns
            .Select(campaign =>
            {
                responsesByCampaign.TryGetValue(campaign.Id, out var campaignResponses);
                campaignResponses ??= [];
                var weekIds = campaignWeekIds[campaign.Id];

                return new TlCampaignOptionDto(
                    campaign.Id,
                    campaign.Name,
                    campaign.Status,
                    campaign.DeletedAtUtc,
                    weekIds
                        .OrderByDescending(item => item)
                        .ToArray(),
                    ResolveQuestions(campaign, campaignResponses));
            })
            .OrderBy(item => item.Name)
            .ToArray();

        var filteredResponseEntities = responses
            .Where(item => selectedWeeks.Count == 0 || CampaignMatchesSelectedWeeks(item.CampaignId, campaignWeekIds, selectedWeeks))
            .Where(item => selectedCampaigns.Count == 0 || selectedCampaigns.Contains(item.CampaignId))
            .Where(item => answerFilters.Count == 0 || MatchesAnswerFilter(item, answerFilters))
            .OrderByDescending(item => item.AnsweredAtUtc)
            .ToArray();
        var employeeReportFieldsLookup = await BuildEmployeeReportFieldsLookupAsync(filteredResponseEntities, cancellationToken);

        var filteredResponses = filteredResponseEntities
            .Select(item =>
            {
                var campaign = campaignLookup[item.CampaignId];
                var week = BuildWeekOption(ToDashboardDate(item.AnsweredAtUtc));
                employeeReportFieldsLookup.TryGetValue(item.EmployeeId.Trim(), out var employeeReportFields);
                return new TlResponseRowDto(
                    item.Id,
                    item.CampaignId,
                    item.QuestionId,
                    week.Id,
                    campaign.Name,
                    item.QuestionText,
                    item.QuestionType,
                    item.NumericValue,
                    item.TextValue,
                    item.UserName,
                    item.Email,
                    item.EmployeeId,
                    item.LeaderSolvoId,
                    item.LeaderFullName,
                    item.LeaderCorporateEmail,
                    employeeReportFields?.InternalEmployeeCategory ?? string.Empty,
                    employeeReportFields?.JobTitle ?? string.Empty,
                    item.Operation,
                    item.EmployeeStatus,
                    item.Department,
                    item.Hostname,
                    item.AnsweredAtUtc);
            })
            .ToArray();

        return new TlDashboardDto(
            session.User.DisplayName,
            session.SolvoId,
            operation,
            operations,
            weekOptions,
            campaignOptions,
            filteredResponses);
    }

    private static bool CampaignMatchesAnyOperation(Domain.Entities.Campaign campaign, IReadOnlyList<string> operations)
        => IsAllOperationsAudience(campaign.Audience) ||
           ParseAudienceOperations(campaign.Audience)
               .Any(item => operations.Contains(item, StringComparer.OrdinalIgnoreCase));

    private static bool CampaignMatchesOperation(Domain.Entities.Campaign campaign, string operation)
        => IsAllOperationsAudience(campaign.Audience) ||
           ParseAudienceOperations(campaign.Audience)
               .Any(item => item.Equals(operation, StringComparison.OrdinalIgnoreCase));

    private static bool ResponseIsInOperationScope(
        Domain.Entities.PulseResponse response,
        Domain.Entities.Campaign campaign,
        IReadOnlyList<string> operations,
        bool includeAllOperations)
    {
        if (includeAllOperations)
        {
            return true;
        }

        if (IsAllOperationsAudience(campaign.Audience))
        {
            return operations.Contains(response.Operation, StringComparer.OrdinalIgnoreCase);
        }

        return CampaignMatchesAnyOperation(campaign, operations);
    }

    private static IReadOnlyList<string> ResolveCampaignWeekIds(
        Domain.Entities.Campaign campaign,
        IReadOnlyDictionary<Guid, Domain.Entities.PulseResponse[]> responsesByCampaign,
        IReadOnlyDictionary<Guid, Domain.Entities.DeliveryLog[]> deliveryLogsByCampaign,
        ISet<string> availableWeekIds)
    {
        var weekIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddWeekId(weekIds, campaign.CreatedAtUtc, availableWeekIds);
        AddWeekId(weekIds, campaign.UpdatedAtUtc, availableWeekIds);

        if (campaign.DeletedAtUtc is DateTimeOffset deletedAtUtc)
        {
            AddWeekId(weekIds, deletedAtUtc, availableWeekIds);
        }

        if (deliveryLogsByCampaign.TryGetValue(campaign.Id, out var deliveryLogs))
        {
            foreach (var deliveryLog in deliveryLogs)
            {
                AddWeekId(weekIds, deliveryLog.PromptedAtUtc, availableWeekIds);
            }
        }

        if (responsesByCampaign.TryGetValue(campaign.Id, out var responses))
        {
            foreach (var response in responses)
            {
                AddWeekId(weekIds, response.AnsweredAtUtc, availableWeekIds);
            }
        }

        return weekIds
            .OrderByDescending(item => item)
            .ToArray();
    }

    private static void AddWeekId(
        ISet<string> weekIds,
        DateTimeOffset value,
        ISet<string> availableWeekIds)
    {
        var weekId = BuildWeekOption(ToDashboardDate(value)).Id;
        if (availableWeekIds.Contains(weekId))
        {
            weekIds.Add(weekId);
        }
    }

    private static bool CampaignMatchesSelectedWeeks(
        Guid campaignId,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> campaignWeekIds,
        ISet<string> selectedWeeks)
        => campaignWeekIds.TryGetValue(campaignId, out var weekIds) &&
           weekIds.Any(selectedWeeks.Contains);

    private static string[] ParseAudienceOperations(string? audience)
    {
        if (string.IsNullOrWhiteSpace(audience) || IsAllOperationsAudience(audience))
        {
            return [];
        }

        return audience
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => item.Length > 0)
            .ToArray();
    }

    private static bool IsAllOperationsAudience(string audience)
    {
        var normalized = audience.Trim();
        return normalized.Equals("Todas las operaciones", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("All operations", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("Todos", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("All", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesAnswerFilter(
        Domain.Entities.PulseResponse response,
        IReadOnlyDictionary<Guid, HashSet<string>> filters)
    {
        if (filters.Count == 0)
        {
            return true;
        }

        if (response.QuestionType == CampaignQuestionType.Text)
        {
            return false;
        }

        var normalizedValues = ResolveResponseFilterValues(response);
        if (normalizedValues.Count == 0)
        {
            return false;
        }

        if (!filters.TryGetValue(response.QuestionId, out var questionValues) || questionValues.Count == 0)
        {
            return false;
        }

        return normalizedValues.Any(questionValues.Contains);
    }

    private async Task<Dictionary<string, EmployeeReportFields>> BuildEmployeeReportFieldsLookupAsync(
        IReadOnlyCollection<Domain.Entities.PulseResponse> responses,
        CancellationToken cancellationToken)
    {
        var employeeIds = responses
            .Select(item => item.EmployeeId.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (employeeIds.Length == 0)
        {
            return new Dictionary<string, EmployeeReportFields>(StringComparer.OrdinalIgnoreCase);
        }

        return new Dictionary<string, EmployeeReportFields>(
            await employeeOperationsProfileResolver.ResolveReportFieldsAsync(employeeIds, cancellationToken),
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ResolveResponseFilterValues(Domain.Entities.PulseResponse response)
    {
        var values = new List<string>(3);
        AddNormalized(values, response.TextValue);

        if (response.NumericValue.HasValue)
        {
            AddNormalized(values, response.NumericValue.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (response.LegacyValue > 0)
        {
            AddNormalized(values, response.LegacyValue.ToString(CultureInfo.InvariantCulture));
        }

        return values;
    }

    private static void AddNormalized(List<string> values, string? value)
    {
        var normalized = NormalizeAnswerValue(value);
        if (normalized.Length > 0 && !values.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            values.Add(normalized);
        }
    }

    private static string NormalizeAnswerValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static IReadOnlyList<TlWeekOptionDto> BuildLatestWeekOptions(DateTimeOffset now, int count)
    {
        var today = ToDashboardDate(now);
        var currentWeekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        return Enumerable
            .Range(0, count)
            .Select(index => BuildWeekOption(currentWeekStart.AddDays(index * -7)))
            .ToArray();
    }

    private static TlWeekOptionDto BuildWeekOption(DateOnly start)
    {
        var end = start.AddDays(6);
        var week = ISOWeek.GetWeekOfYear(start.ToDateTime(TimeOnly.MinValue));
        return new TlWeekOptionDto($"{start:yyyy-MM-dd}", $"Week {week} ({start:MMM d} - {end:MMM d})", start, end);
    }

    private static DateOnly ToDashboardDate(DateTimeOffset value)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, DashboardTimeZone).DateTime);

    private static DateTimeOffset ToDashboardStartUtc(DateOnly value)
    {
        var localStart = value.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, DashboardTimeZone), TimeSpan.Zero);
    }

    private static TimeZoneInfo ResolveDashboardTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
        }
    }

    private static IReadOnlyList<TlQuestionOptionDto> ResolveQuestions(
        Domain.Entities.Campaign campaign,
        IReadOnlyList<Domain.Entities.PulseResponse> responses)
    {
        return campaign.ToDto().Questions
            .Select(question =>
            {
                var options = question.Type switch
                {
                    CampaignQuestionType.Scale => Enumerable
                        .Range(question.MinValue ?? 1, Math.Max(0, (question.MaxValue ?? 5) - (question.MinValue ?? 1) + 1))
                        .Select(item => item.ToString(CultureInfo.InvariantCulture))
                        .ToArray(),
                    CampaignQuestionType.YesNo => ["Si", "No"],
                    CampaignQuestionType.Choice => question.Options ?? [],
                    _ => Array.Empty<string>()
                };

                if (question.Type is CampaignQuestionType.YesNo or CampaignQuestionType.Choice)
                {
                    var responseOptions = responses
                        .Where(item => item.QuestionId == question.Id)
                        .Select(item => item.TextValue?.Trim() ?? string.Empty)
                        .Where(item => item.Length > 0)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(item => item)
                        .ToArray();

                    if (responseOptions.Length > 0)
                    {
                        options = responseOptions;
                    }
                }

                return new TlQuestionOptionDto(
                    question.Id,
                    question.Text,
                    question.Type,
                    question.MinValue,
                    question.MaxValue,
                    options);
            })
            .ToArray();
    }
}
