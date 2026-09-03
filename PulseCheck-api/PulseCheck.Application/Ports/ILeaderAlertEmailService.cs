namespace PulseCheck.Application.Ports;

public interface ILeaderAlertEmailService
{
    Task SendLockedDeviceAlertAsync(LeaderLockAlertEmailRequest request, CancellationToken cancellationToken);
}

public sealed record LeaderLockAlertEmailRequest(
    string RecipientEmail,
    IReadOnlyList<string> AdditionalRecipientEmails,
    string LeaderName,
    string EmployeeName,
    string EmployeeEmail,
    string EmployeeId,
    string Operation,
    string EmployeeStatus,
    string Department,
    string LockReason,
    int? IdleSecondsAtLock,
    int LockedMinutes,
    DateTimeOffset LockedAtUtc,
    DateTimeOffset AlertedAtUtc,
    DateTimeOffset LockedAtLocal,
    DateTimeOffset AlertedAtLocal);
