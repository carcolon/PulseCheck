using PulseCheck.Application.Ports;

namespace PulseCheck.Infrastructure.Services;

public sealed class NullLeaderAlertEmailService : ILeaderAlertEmailService
{
    public Task SendLockedDeviceAlertAsync(LeaderLockAlertEmailRequest request, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
