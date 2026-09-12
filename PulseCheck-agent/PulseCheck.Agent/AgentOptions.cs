namespace PulseCheck.Agent;

public sealed class AgentOptions
{
    public const string SectionName = "PulseCheckAgent";

    public string BaseUrl { get; init; } = "https://localhost:7059";

    public string ProvisioningToken { get; init; } = string.Empty;

    public string UserId { get; init; } = "u-agent-demo";

    public string UserName { get; init; } = "Usuario Demo";

    public string Email { get; init; } = string.Empty;

    public string Department { get; init; } = string.Empty;

    public string DeviceId { get; init; } = Environment.MachineName.ToLowerInvariant();

    public string Hostname { get; init; } = Environment.MachineName;

    public string OperatingSystem { get; init; } = Environment.OSVersion.VersionString;

    public string AgentVersion { get; init; } = "0.1.0";

    public int PollingIntervalSeconds { get; init; } = 30;

    public bool AutoUpdateEnabled { get; init; } = false;

    public string UpdateFeedUrl { get; init; } = string.Empty;

    public int UpdateCheckIntervalMinutes { get; init; } = 60;

    public string LegacyUpdateManifestUrl { get; init; } = string.Empty;

    public int IdleLockThresholdSeconds { get; init; } = 60;

    public int LockedFirstAlertThresholdMinutes { get; init; } = 10;

    public int LockedAlertThresholdMinutes { get; init; } = 30;

    public int LockedDurationReportIntervalMinutes { get; init; } = 1;

    public bool SimulateResponses { get; init; } = false;
}
