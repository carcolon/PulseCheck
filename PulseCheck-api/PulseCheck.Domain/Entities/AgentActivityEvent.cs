namespace PulseCheck.Domain.Entities;

public sealed class AgentActivityEvent
{
    public Guid Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? LockReason { get; set; }
    public int? IdleSecondsAtLock { get; set; }
    public int? DurationSeconds { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public DateTimeOffset? OccurredAtLocal { get; set; }
}
