namespace PulseCheck.Domain.Entities;

public sealed class AdminUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? EntraObjectId { get; set; }
    public string? TenantId { get; set; }
    public string AuthenticationMode { get; set; } = "Local";
    public string Role { get; set; } = "Admin";
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? LastLoginAtUtc { get; set; }
}
