namespace PulseCheck.Domain.Entities;

public sealed class Device
{
    public Guid Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
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
    public string Client { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = string.Empty;
    public DateTimeOffset FirstSeenAtUtc { get; set; }
    public DateTimeOffset LastSeenAtUtc { get; set; }
}
