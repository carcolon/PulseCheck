namespace PulseCheck.Domain.Entities;

public sealed class AdminSession
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }
    public AdminUser? AdminUser { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
