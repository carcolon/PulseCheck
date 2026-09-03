namespace PulseCheck.Api.Models;

public sealed class PulseCheckOptions
{
    public const string SectionName = "PulseCheck";

    public string EnvironmentName { get; init; } = "Local";

    public string DatabaseProvider { get; init; } = "Sqlite";

    public string[] AllowedOrigins { get; init; } = [];

    public bool AllowLocalAdminLogin { get; init; } = true;

    public RateLimitOptions RateLimits { get; init; } = new();

    public AgentSecurityOptions AgentSecurity { get; init; } = new();

    public EntraOptions Entra { get; init; } = new();

    public FabricOptions Fabric { get; init; } = new();

    public EmployeeProfileBackfillOptions EmployeeProfileBackfill { get; init; } = new();

    public TransformationalLeaderAssignmentSyncOptions TransformationalLeaderAssignmentSync { get; init; } = new();
}

public sealed class RateLimitOptions
{
    public int AuthPermitLimitPerMinute { get; init; } = 60;

    public int AdminApiPermitLimitPerMinute { get; init; } = 300;

    public int AgentApiPermitLimitPerMinute { get; init; } = 300;
}

public sealed class AgentSecurityOptions
{
    public string ProvisioningToken { get; init; } = string.Empty;

    public int CredentialLifetimeDays { get; init; } = 180;

    public int CredentialSlidingRenewalDays { get; init; } = 30;
}

public sealed class EntraOptions
{
    public bool Enabled { get; init; }

    public bool GraphEnabled { get; init; }

    public string TenantId { get; init; } = string.Empty;

    public string TenantDomain { get; init; } = string.Empty;

    public string ApiClientId { get; init; } = string.Empty;

    public string WebClientId { get; init; } = string.Empty;

    public string GraphClientId { get; init; } = string.Empty;

    public string GraphClientSecret { get; init; } = string.Empty;

    public int AuthorizationCodeLifetimeMinutes { get; init; } = 10;
}

public sealed class FabricOptions
{
    public bool Enabled { get; init; }

    public string Endpoint { get; init; } = string.Empty;

    public string Database { get; init; } = string.Empty;

    public int CommandTimeoutSeconds { get; init; } = 30;
}

public sealed class EmployeeProfileBackfillOptions
{
    public bool Enabled { get; init; }

    public int MaxDevicesPerRun { get; init; } = 200;

    public bool UpdateHistoricalResponses { get; init; } = true;

    public int StartupDelaySeconds { get; init; } = 15;
}

public sealed class TransformationalLeaderAssignmentSyncOptions
{
    public bool Enabled { get; init; } = true;

    public int DailyRunHourUtc { get; init; } = 8;
}
