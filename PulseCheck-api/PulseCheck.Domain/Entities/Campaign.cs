using PulseCheck.Domain.Enums;

namespace PulseCheck.Domain.Entities;

public sealed class Campaign
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string ScheduleRule { get; set; } = string.Empty;
    public TimeOnly DeliveryWindowStart { get; set; }
    public TimeOnly DeliveryWindowEnd { get; set; }
    public CampaignStatus Status { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public int MinValue { get; set; } = 1;
    public int MaxValue { get; set; } = 5;
    public string QuestionsJson { get; set; } = "[]";
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
}
