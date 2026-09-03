using System.Security.Cryptography;
using System.Text;

namespace PulseCheck.Agent;

public sealed class AgentState
{
    public Dictionary<Guid, CampaignLocalState> Campaigns { get; init; } = [];
}

public sealed class CampaignLocalState
{
    public DateTimeOffset? LastPromptedAtUtc { get; set; }

    public DateTimeOffset? LastAnsweredAtUtc { get; set; }

    public DateTimeOffset? PostponedUntilUtc { get; set; }

    public int PostponeCount { get; set; }

    public string? LastPromptedCampaignSignature { get; set; }

    public string? LastAnsweredCampaignSignature { get; set; }
}

public static class CampaignScheduler
{
    public static bool BackfillImmediateCampaignSignature(AgentCampaignConfiguration campaign, CampaignLocalState state)
    {
        var parsedRule = ParseRule(campaign.ScheduleRule);
        if (parsedRule.Frequency != "immediate")
        {
            return false;
        }

        var signature = GetCampaignContentSignature(campaign);
        var updated = false;

        if (state.LastPromptedAtUtc is not null && string.IsNullOrWhiteSpace(state.LastPromptedCampaignSignature))
        {
            state.LastPromptedCampaignSignature = signature;
            updated = true;
        }

        if (state.LastAnsweredAtUtc is not null && string.IsNullOrWhiteSpace(state.LastAnsweredCampaignSignature))
        {
            state.LastAnsweredCampaignSignature = signature;
            updated = true;
        }

        return updated;
    }

    public static string GetCampaignContentSignature(AgentCampaignConfiguration campaign)
    {
        var builder = new StringBuilder();
        AppendSignatureValue(builder, campaign.Name);
        AppendSignatureValue(builder, campaign.Audience);
        AppendSignatureValue(builder, campaign.ScheduleRule);
        AppendSignatureValue(builder, campaign.DeliveryWindowStart);
        AppendSignatureValue(builder, campaign.DeliveryWindowEnd);
        AppendSignatureValue(builder, campaign.Questions.Count.ToString());

        foreach (var question in campaign.Questions)
        {
            AppendSignatureValue(builder, question.Text);
            AppendSignatureValue(builder, question.Type);
            AppendSignatureValue(builder, question.MinValue?.ToString());
            AppendSignatureValue(builder, question.MaxValue?.ToString());
            AppendSignatureValue(builder, question.Placeholder);
            AppendSignatureValue(builder, question.Options?.Count.ToString());

            foreach (var option in question.Options ?? [])
            {
                AppendSignatureValue(builder, option);
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    public static bool HasAnswerForCurrentCampaign(AgentCampaignConfiguration campaign, CampaignLocalState state)
    {
        var parsedRule = ParseRule(campaign.ScheduleRule);
        if (parsedRule.Frequency == "immediate")
        {
            return WasAnsweredForCurrentCampaignContent(campaign, state);
        }

        return state.LastAnsweredAtUtc is not null;
    }

    public static bool IsDue(AgentCampaignConfiguration campaign, CampaignLocalState? state, DateTimeOffset now)
    {
        var parsedRule = ParseRule(campaign.ScheduleRule);

        if (ShouldSuppressAfterAnswer(parsedRule, campaign, state, now))
        {
            return false;
        }

        if (!string.Equals(campaign.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!IsWithinWindow(campaign, now))
        {
            return false;
        }

        if (state?.PostponedUntilUtc is DateTimeOffset postponedUntilUtc)
        {
            if (parsedRule.Frequency != "immediate" || WasPromptedForCurrentCampaignContent(campaign, state))
            {
                return postponedUntilUtc <= now.ToUniversalTime();
            }
        }

        if (!MatchesRule(parsedRule.CronExpression, now))
        {
            return false;
        }

        if (!MatchesFrequencyInterval(parsedRule, campaign, now))
        {
            return false;
        }

        return !WasPromptedInCurrentSlot(parsedRule, campaign, state, now);
    }

    private static bool ShouldSuppressAfterAnswer(
        ParsedRule parsedRule,
        AgentCampaignConfiguration campaign,
        CampaignLocalState? state,
        DateTimeOffset now)
    {
        if (state?.LastAnsweredAtUtc is not DateTimeOffset lastAnsweredAtUtc)
        {
            return false;
        }

        if (parsedRule.Frequency == "immediate")
        {
            return WasAnsweredForCurrentCampaignContent(campaign, state);
        }

        return BuildOccurrenceKey(parsedRule, campaign, lastAnsweredAtUtc.ToLocalTime()) ==
               BuildOccurrenceKey(parsedRule, campaign, now);
    }

    private static bool IsWithinWindow(AgentCampaignConfiguration campaign, DateTimeOffset now)
    {
        if (!TimeOnly.TryParse(campaign.DeliveryWindowStart, out var start) ||
            !TimeOnly.TryParse(campaign.DeliveryWindowEnd, out var end))
        {
            return true;
        }

        var current = TimeOnly.FromDateTime(now.LocalDateTime);
        return current >= start && current <= end;
    }

    private static bool MatchesRule(string rule, DateTimeOffset now)
    {
        var parts = rule.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 6)
        {
            return true;
        }

        var minute = parts[1];
        var hour = parts[2];
        var dayOfWeek = parts[5];

        return MatchesNumber(minute, now.Minute) &&
               MatchesNumber(hour, now.Hour) &&
               MatchesDayOfWeek(dayOfWeek, now.DayOfWeek);
    }

    private static bool MatchesFrequencyInterval(ParsedRule parsedRule, AgentCampaignConfiguration campaign, DateTimeOffset now)
    {
        var currentDate = DateOnly.FromDateTime(now.LocalDateTime.Date);
        var anchorDate = DateOnly.FromDateTime(campaign.CreatedAtUtc.ToLocalTime().DateTime.Date);

        return parsedRule.Frequency switch
        {
            "biweekly" => GetWeekDistance(anchorDate, currentDate) % 2 == 0,
            "quarterly" => MatchesCalendarMonthInterval(anchorDate, currentDate, 3),
            "monthly" => MatchesCalendarMonthInterval(anchorDate, currentDate, 1),
            "weekly" => true,
            _ => true
        };
    }

    private static bool WasPromptedInCurrentSlot(
        ParsedRule parsedRule,
        AgentCampaignConfiguration campaign,
        CampaignLocalState? state,
        DateTimeOffset now)
    {
        if (state?.LastPromptedAtUtc is not DateTimeOffset lastPromptedAtUtc)
        {
            return false;
        }

        if (parsedRule.Frequency == "immediate")
        {
            return WasPromptedForCurrentCampaignContent(campaign, state);
        }

        var lastPromptedAtLocal = lastPromptedAtUtc.ToLocalTime();
        return BuildSlotKey(parsedRule, lastPromptedAtLocal) == BuildSlotKey(parsedRule, now);
    }

    private static bool WasPromptedForCurrentCampaignContent(
        AgentCampaignConfiguration campaign,
        CampaignLocalState? state)
        => state?.LastPromptedCampaignSignature is string signature &&
           string.Equals(signature, GetCampaignContentSignature(campaign), StringComparison.Ordinal);

    private static bool WasAnsweredForCurrentCampaignContent(
        AgentCampaignConfiguration campaign,
        CampaignLocalState? state)
        => state?.LastAnsweredCampaignSignature is string signature &&
           string.Equals(signature, GetCampaignContentSignature(campaign), StringComparison.Ordinal);

    private static void AppendSignatureValue(StringBuilder builder, string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        builder
            .Append(normalized.Length)
            .Append(':')
            .Append(normalized)
            .Append('|');
    }

    private static string BuildSlotKey(ParsedRule parsedRule, DateTimeOffset value)
    {
        var local = value.LocalDateTime;
        return parsedRule.Frequency switch
        {
            "hourly" => local.ToString("yyyyMMddHH"),
            _ => local.ToString("yyyyMMddHHmm")
        };
    }

    private static string BuildOccurrenceKey(
        ParsedRule parsedRule,
        AgentCampaignConfiguration campaign,
        DateTimeOffset value)
    {
        var localDate = DateOnly.FromDateTime(value.LocalDateTime.Date);
        var anchorDate = DateOnly.FromDateTime(campaign.CreatedAtUtc.ToLocalTime().DateTime.Date);

        return parsedRule.Frequency switch
        {
            "immediate" => "immediate",
            "hourly" => value.LocalDateTime.ToString("yyyyMMddHH"),
            "weekly" => $"week:{StartOfWeek(localDate):yyyyMMdd}",
            "biweekly" => $"biweek:{GetWeekDistance(anchorDate, localDate) / 2}",
            "monthly" => value.LocalDateTime.ToString("yyyyMM"),
            "quarterly" => $"quarter:{value.LocalDateTime.Year}:{((value.LocalDateTime.Month - 1) / 3) + 1}",
            _ => value.LocalDateTime.ToString("yyyyMMdd")
        };
    }

    private static int GetWeekDistance(DateOnly anchorDate, DateOnly currentDate)
    {
        var anchorWeekStart = StartOfWeek(anchorDate);
        var currentWeekStart = StartOfWeek(currentDate);
        return Math.Max(0, (currentWeekStart.DayNumber - anchorWeekStart.DayNumber) / 7);
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var offset = date.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)date.DayOfWeek - 1;
        return date.AddDays(-offset);
    }

    private static int GetMonthDistance(DateOnly anchorDate, DateOnly currentDate)
        => Math.Max(0, ((currentDate.Year - anchorDate.Year) * 12) + currentDate.Month - anchorDate.Month);

    private static bool MatchesCalendarMonthInterval(DateOnly anchorDate, DateOnly currentDate, int monthInterval)
    {
        var monthDistance = GetMonthDistance(anchorDate, currentDate);
        if (monthDistance % monthInterval != 0)
        {
            return false;
        }

        var anchorOrdinal = GetWeekdayOrdinalInMonth(anchorDate);
        var currentOrdinal = GetWeekdayOrdinalInMonth(currentDate);
        var lastOrdinalForCurrentWeekday = GetLastWeekdayOrdinalInMonth(currentDate.Year, currentDate.Month, currentDate.DayOfWeek);

        return currentOrdinal == Math.Min(anchorOrdinal, lastOrdinalForCurrentWeekday);
    }

    private static int GetWeekdayOrdinalInMonth(DateOnly date)
        => ((date.Day - 1) / 7) + 1;

    private static int GetLastWeekdayOrdinalInMonth(int year, int month, DayOfWeek dayOfWeek)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        for (var day = daysInMonth; day >= 1; day--)
        {
            var candidate = new DateOnly(year, month, day);
            if (candidate.DayOfWeek == dayOfWeek)
            {
                return GetWeekdayOrdinalInMonth(candidate);
            }
        }

        return 1;
    }

    private static bool MatchesNumber(string token, int value)
    {
        if (token is "*" or "?")
        {
            return true;
        }

        return int.TryParse(token, out var parsed) && parsed == value;
    }

    private static bool MatchesDayOfWeek(string token, DayOfWeek dayOfWeek)
    {
        if (token is "*" or "?")
        {
            return true;
        }

        var current = dayOfWeek switch
        {
            DayOfWeek.Monday => "MON",
            DayOfWeek.Tuesday => "TUE",
            DayOfWeek.Wednesday => "WED",
            DayOfWeek.Thursday => "THU",
            DayOfWeek.Friday => "FRI",
            DayOfWeek.Saturday => "SAT",
            _ => "SUN"
        };

        var dayTokens = token
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static item => item.ToUpperInvariant())
            .ToArray();

        if (dayTokens.Length == 0)
        {
            return false;
        }

        return dayTokens.Any(tokenPart => MatchesSingleDayToken(tokenPart, current));
    }

    private static bool MatchesSingleDayToken(string token, string current)
    {
        if (token is "*" or "?")
        {
            return true;
        }

        if (token.Contains('-'))
        {
            var range = token.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (range.Length != 2)
            {
                return false;
            }

            var ordered = new[] { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN" };
            var startIndex = Array.IndexOf(ordered, range[0].ToUpperInvariant());
            var endIndex = Array.IndexOf(ordered, range[1].ToUpperInvariant());
            var currentIndex = Array.IndexOf(ordered, current);

            if (startIndex < 0 || endIndex < 0 || currentIndex < 0)
            {
                return false;
            }

            return startIndex <= endIndex
                ? currentIndex >= startIndex && currentIndex <= endIndex
                : currentIndex >= startIndex || currentIndex <= endIndex;
        }

        return string.Equals(token, current, StringComparison.OrdinalIgnoreCase);
    }

    private static ParsedRule ParseRule(string rawRule)
    {
        var split = rawRule.Split('#', 2, StringSplitOptions.TrimEntries);
        var cronExpression = split[0].Trim();
        if (split.Length == 1)
        {
            return new ParsedRule(cronExpression, InferFrequency(cronExpression));
        }

        var metadata = split[1];
        var metadataFlags = metadata
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
        var frequency = metadataFlags
            .FirstOrDefault(item => item.StartsWith("freq=", StringComparison.OrdinalIgnoreCase))?
            .Split('=', 2)[1]
            .Trim()
            .ToLowerInvariant();

        return new ParsedRule(cronExpression, NormalizeFrequency(frequency, cronExpression));
    }

    private static string InferFrequency(string cronExpression)
    {
        var parts = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 3 && parts[1] == "*" && parts[2] == "*"
            ? "immediate"
            : parts.Length >= 3 && parts[2] == "*"
                ? "hourly"
                : "custom";
    }

    private static string NormalizeFrequency(string? frequency, string cronExpression)
        => frequency is "immediate" or "hourly" or "custom" or "weekly" or "biweekly" or "monthly" or "quarterly"
            ? frequency
            : InferFrequency(cronExpression);

    private sealed record ParsedRule(string CronExpression, string Frequency);
}
