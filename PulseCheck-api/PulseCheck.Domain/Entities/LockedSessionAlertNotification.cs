namespace PulseCheck.Domain.Entities;

public sealed class LockedSessionAlertNotification
{
    public Guid Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string Client { get; set; } = string.Empty;
    public DateTimeOffset LockedAtUtc { get; set; }
    public int ThresholdMinutes { get; set; }
    public DateTimeOffset SentAtUtc { get; set; }
}
