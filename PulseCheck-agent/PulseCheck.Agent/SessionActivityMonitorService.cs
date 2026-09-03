using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32;

namespace PulseCheck.Agent;

public sealed class SessionActivityMonitorService(
    ILogger<SessionActivityMonitorService> logger,
    AgentActivityQueueService activityQueue,
    AgentIdentityResolver identityResolver,
    IOptions<AgentOptions> options) : IHostedService
{
    private readonly AgentOptions settings = options.Value;
    private LockSnapshot? currentLock;
    private SuspendSnapshot? currentSuspend;
    private readonly List<CancellationTokenSource> lockedAlertCancellations = [];
    private bool subscribed;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var identity = identityResolver.Resolve(settings);
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionEnding += OnSessionEnding;
        subscribed = true;
        DiagnosticLog.Write("Session activity monitor started.");
        logger.LogInformation("Session activity monitor started for device {DeviceId}.", identity.DeviceId);
        _ = TrackDeviceStartedAsync();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (subscribed)
        {
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionEnding -= OnSessionEnding;
            subscribed = false;
        }

        CancelLockedAlertTimers();
        DiagnosticLog.Write("Session activity monitor stopped.");
        return Task.CompletedTask;
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs args)
    {
        _ = args.Reason switch
        {
            SessionSwitchReason.SessionLock => HandleSessionLockAsync(),
            SessionSwitchReason.SessionUnlock => HandleSessionUnlockAsync(),
            _ => Task.CompletedTask
        };
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs args)
    {
        _ = args.Mode switch
        {
            PowerModes.Suspend => HandleSuspendAsync(),
            PowerModes.Resume => HandleResumeAsync(),
            _ => Task.CompletedTask
        };
    }

    private void OnSessionEnding(object sender, SessionEndingEventArgs args)
    {
        _ = TrackDeviceShutdownAsync(args.Reason.ToString());
    }

    private async Task TrackDeviceStartedAsync()
    {
        try
        {
            var runtimeIdentity = identityResolver.Resolve(settings);
            var occurredAtUtc = DateTimeOffset.UtcNow;
            DiagnosticLog.Write("Device start detected.");

            await activityQueue.EnqueueAndFlushAsync(
                new AgentActivityEventRequest(
                    runtimeIdentity.DeviceId,
                    runtimeIdentity.UserId,
                    runtimeIdentity.UserName,
                    runtimeIdentity.Email,
                    runtimeIdentity.Department,
                    runtimeIdentity.Hostname,
                    "DeviceStarted",
                    "AgentStart",
                    null,
                    null,
                    occurredAtUtc,
                    null,
                    null,
                    ToLocalOffset(occurredAtUtc)),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Device start tracking failed: {exception.Message}");
            logger.LogWarning(exception, "Device start tracking failed.");
        }
    }

    private async Task TrackDeviceShutdownAsync(string reason)
    {
        try
        {
            var runtimeIdentity = identityResolver.Resolve(settings);
            var occurredAtUtc = DateTimeOffset.UtcNow;
            DiagnosticLog.Write($"Device shutdown detected. Reason={reason}.");

            await activityQueue.EnqueueAndFlushAsync(
                new AgentActivityEventRequest(
                    runtimeIdentity.DeviceId,
                    runtimeIdentity.UserId,
                    runtimeIdentity.UserName,
                    runtimeIdentity.Email,
                    runtimeIdentity.Department,
                    runtimeIdentity.Hostname,
                    "DeviceShutdown",
                    reason,
                    GetIdleSeconds(),
                    null,
                    occurredAtUtc,
                    null,
                    null,
                    ToLocalOffset(occurredAtUtc)),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Device shutdown tracking failed: {exception.Message}");
            logger.LogWarning(exception, "Device shutdown tracking failed.");
        }
    }

    private async Task HandleSessionLockAsync()
    {
        try
        {
            var runtimeIdentity = identityResolver.Resolve(settings);
            var idleSeconds = GetIdleSeconds();
            var reason = idleSeconds >= settings.IdleLockThresholdSeconds ? "AutoLock" : "ManualLock";
            var occurredAtUtc = DateTimeOffset.UtcNow;
            var occurredAtLocal = ToLocalOffset(occurredAtUtc);

            currentLock = new LockSnapshot(runtimeIdentity, occurredAtUtc, occurredAtLocal, idleSeconds, reason);
            ScheduleLockedAlerts(currentLock);

            DiagnosticLog.Write($"Session lock detected. Reason={reason}. IdleSeconds={idleSeconds}.");
            logger.LogInformation(
                "Session lock detected for device {DeviceId}. Reason={Reason}. IdleSeconds={IdleSeconds}.",
                runtimeIdentity.DeviceId,
                reason,
                idleSeconds);

            await activityQueue.EnqueueAndFlushAsync(
                new AgentActivityEventRequest(
                    runtimeIdentity.DeviceId,
                    runtimeIdentity.UserId,
                    runtimeIdentity.UserName,
                    runtimeIdentity.Email,
                    runtimeIdentity.Department,
                    runtimeIdentity.Hostname,
                    "SessionLocked",
                    reason,
                    idleSeconds,
                    null,
                    occurredAtUtc,
                    null,
                    null,
                    occurredAtLocal),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Session lock tracking failed: {exception.Message}");
            logger.LogWarning(exception, "Session lock tracking failed.");
        }
    }

    private async Task HandleSessionUnlockAsync()
    {
        try
        {
            var runtimeIdentity = identityResolver.Resolve(settings);
            var occurredAtUtc = DateTimeOffset.UtcNow;
            var occurredAtLocal = ToLocalOffset(occurredAtUtc);
            var lockSnapshot = currentLock;
            currentLock = null;
            CancelLockedAlertTimers();
            var durationSeconds = lockSnapshot is null
                ? (int?)null
                : Math.Max(0, (int)Math.Round((occurredAtUtc - lockSnapshot.LockedAtUtc).TotalSeconds));

            DiagnosticLog.Write(
                durationSeconds is int duration
                    ? $"Session unlock detected. LockedFor={duration}s."
                    : "Session unlock detected.");
            logger.LogInformation(
                "Session unlock detected for device {DeviceId}. LockedForSeconds={DurationSeconds}.",
                runtimeIdentity.DeviceId,
                durationSeconds);

            await activityQueue.EnqueueAndFlushAsync(
                new AgentActivityEventRequest(
                    runtimeIdentity.DeviceId,
                    runtimeIdentity.UserId,
                    runtimeIdentity.UserName,
                    runtimeIdentity.Email,
                    runtimeIdentity.Department,
                    runtimeIdentity.Hostname,
                    "SessionUnlocked",
                    lockSnapshot?.Reason,
                    lockSnapshot?.IdleSecondsAtLock,
                    durationSeconds,
                    occurredAtUtc,
                    lockSnapshot?.LockedAtUtc,
                    lockSnapshot?.LockedAtLocal,
                    occurredAtLocal),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Session unlock tracking failed: {exception.Message}");
            logger.LogWarning(exception, "Session unlock tracking failed.");
        }
    }

    private async Task HandleSuspendAsync()
    {
        try
        {
            var runtimeIdentity = identityResolver.Resolve(settings);
            var idleSeconds = GetIdleSeconds();
            var occurredAtUtc = DateTimeOffset.UtcNow;
            var occurredAtLocal = ToLocalOffset(occurredAtUtc);
            currentSuspend = new SuspendSnapshot(occurredAtUtc, occurredAtLocal, idleSeconds);

            DiagnosticLog.Write($"Power suspend detected. IdleSeconds={idleSeconds}.");
            logger.LogInformation(
                "Power suspend detected for device {DeviceId}. IdleSeconds={IdleSeconds}.",
                runtimeIdentity.DeviceId,
                idleSeconds);

            await activityQueue.EnqueueAndFlushAsync(
                new AgentActivityEventRequest(
                    runtimeIdentity.DeviceId,
                    runtimeIdentity.UserId,
                    runtimeIdentity.UserName,
                    runtimeIdentity.Email,
                    runtimeIdentity.Department,
                    runtimeIdentity.Hostname,
                    "DeviceSuspended",
                    "PowerSuspend",
                    idleSeconds,
                    null,
                    occurredAtUtc,
                    null,
                    null,
                    occurredAtLocal),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Power suspend tracking failed: {exception.Message}");
            logger.LogWarning(exception, "Power suspend tracking failed.");
        }
    }

    private async Task HandleResumeAsync()
    {
        try
        {
            var runtimeIdentity = identityResolver.Resolve(settings);
            var occurredAtUtc = DateTimeOffset.UtcNow;
            var occurredAtLocal = ToLocalOffset(occurredAtUtc);
            var suspendSnapshot = currentSuspend;
            currentSuspend = null;
            var durationSeconds = suspendSnapshot is null
                ? (int?)null
                : Math.Max(0, (int)Math.Round((occurredAtUtc - suspendSnapshot.SuspendedAtUtc).TotalSeconds));

            DiagnosticLog.Write(
                durationSeconds is int duration
                    ? $"Power resume detected. SuspendedFor={duration}s."
                    : "Power resume detected.");
            logger.LogInformation(
                "Power resume detected for device {DeviceId}. SuspendedForSeconds={DurationSeconds}.",
                runtimeIdentity.DeviceId,
                durationSeconds);

            await activityQueue.EnqueueAndFlushAsync(
                new AgentActivityEventRequest(
                    runtimeIdentity.DeviceId,
                    runtimeIdentity.UserId,
                    runtimeIdentity.UserName,
                    runtimeIdentity.Email,
                    runtimeIdentity.Department,
                    runtimeIdentity.Hostname,
                    "DeviceResumed",
                    "PowerSuspend",
                    suspendSnapshot?.IdleSecondsBeforeSuspend,
                    durationSeconds,
                    occurredAtUtc,
                    suspendSnapshot?.SuspendedAtUtc,
                    suspendSnapshot?.SuspendedAtLocal,
                    occurredAtLocal),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Power resume tracking failed: {exception.Message}");
            logger.LogWarning(exception, "Power resume tracking failed.");
        }
    }

    private void ScheduleLockedAlerts(LockSnapshot lockSnapshot)
    {
        CancelLockedAlertTimers();

        ScheduleLockedDurationReports(lockSnapshot);
    }

    private void ScheduleLockedDurationReports(LockSnapshot lockSnapshot)
    {
        var reportInterval = TimeSpan.FromMinutes(Math.Max(1, settings.LockedDurationReportIntervalMinutes));
        var cancellation = new CancellationTokenSource();
        lockedAlertCancellations.Add(cancellation);

        _ = Task.Run(async () =>
        {
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    await Task.Delay(reportInterval, cancellation.Token);

                    if (currentLock != lockSnapshot || cancellation.IsCancellationRequested)
                    {
                        return;
                    }

                    var occurredAtUtc = DateTimeOffset.UtcNow;
                    var actualDurationSeconds = Math.Max(0, (int)Math.Round((occurredAtUtc - lockSnapshot.LockedAtUtc).TotalSeconds));

                    DiagnosticLog.Write(
                        $"Session locked duration observed. LockedFor={actualDurationSeconds}s. Reason={lockSnapshot.Reason}.");
                    logger.LogInformation(
                        "Session locked duration observed for device {DeviceId}. LockedForSeconds={DurationSeconds}. Reason={Reason}.",
                        lockSnapshot.Identity.DeviceId,
                        actualDurationSeconds,
                        lockSnapshot.Reason);

                    await activityQueue.EnqueueAndFlushAsync(
                        new AgentActivityEventRequest(
                            lockSnapshot.Identity.DeviceId,
                            lockSnapshot.Identity.UserId,
                            lockSnapshot.Identity.UserName,
                            lockSnapshot.Identity.Email,
                            lockSnapshot.Identity.Department,
                            lockSnapshot.Identity.Hostname,
                            "SessionLockedDurationObserved",
                            lockSnapshot.Reason,
                            lockSnapshot.IdleSecondsAtLock,
                            actualDurationSeconds,
                            occurredAtUtc,
                            lockSnapshot.LockedAtUtc,
                            lockSnapshot.LockedAtLocal,
                            ToLocalOffset(occurredAtUtc)),
                        CancellationToken.None);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when the user unlocks before the next observation tick.
            }
            catch (Exception exception)
            {
                DiagnosticLog.Write($"Session locked duration observation failed: {exception.Message}");
                logger.LogWarning(exception, "Session locked duration observation failed.");
            }
        });
    }

    private void CancelLockedAlertTimers()
    {
        if (lockedAlertCancellations.Count == 0)
        {
            return;
        }

        foreach (var cancellation in lockedAlertCancellations.ToArray())
        {
            try
            {
                cancellation.Cancel();
            }
            finally
            {
                cancellation.Dispose();
            }
        }

        lockedAlertCancellations.Clear();
    }

    private static DateTimeOffset ToLocalOffset(DateTimeOffset utcValue)
        => TimeZoneInfo.ConvertTime(utcValue, TimeZoneInfo.Local);

    private static int GetIdleSeconds()
    {
        var lastInputInfo = new LASTINPUTINFO
        {
            cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>()
        };

        if (!GetLastInputInfo(ref lastInputInfo))
        {
            return 0;
        }

        var tickCount = Environment.TickCount64;
        var idleMilliseconds = Math.Max(0, tickCount - lastInputInfo.dwTime);
        return (int)Math.Floor(idleMilliseconds / 1000d);
    }

    private sealed record LockSnapshot(AgentRuntimeIdentity Identity, DateTimeOffset LockedAtUtc, DateTimeOffset LockedAtLocal, int IdleSecondsAtLock, string Reason);
    private sealed record SuspendSnapshot(DateTimeOffset SuspendedAtUtc, DateTimeOffset SuspendedAtLocal, int IdleSecondsBeforeSuspend);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
}
