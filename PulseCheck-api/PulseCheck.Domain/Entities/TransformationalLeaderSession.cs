namespace PulseCheck.Domain.Entities;

public sealed class TransformationalLeaderSession
{
    public Guid Id { get; set; }
    public string SolvoId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string OperationsJson { get; set; } = "[]";
    public string TokenHash { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
