using PulseCheck.Domain.Enums;

namespace PulseCheck.Domain.Entities;

public sealed class PulseResponse
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public Guid QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public CampaignQuestionType QuestionType { get; set; } = CampaignQuestionType.Scale;
    public string DeviceId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string EntraObjectId { get; set; } = string.Empty;
    public string UserPrincipalName { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string EmployeeStatus { get; set; } = string.Empty;
    public string LeaderSolvoId { get; set; } = string.Empty;
    public string LeaderFullName { get; set; } = string.Empty;
    public string LeaderCorporateEmail { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public int LegacyValue { get; set; }
    public int? NumericValue { get; set; }
    public string? TextValue { get; set; }
    public Guid SubmissionId { get; set; }
    public DateTimeOffset AnsweredAtUtc { get; set; }
}
