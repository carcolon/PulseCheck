namespace PulseCheck.Domain.Entities;

public sealed class ClientInactivityAlertSetting
{
    public Guid Id { get; set; }
    public string Client { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public int AlertThresholdMinutes { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string AdditionalRecipientEmailsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
