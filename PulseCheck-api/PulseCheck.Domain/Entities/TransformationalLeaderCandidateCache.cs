namespace PulseCheck.Domain.Entities;

public sealed class TransformationalLeaderCandidateCache
{
    public Guid Id { get; set; }
    public string SolvoId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string CorporateEmail { get; set; } = string.Empty;
    public string JobTitleCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string Client { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset LastSyncedAtUtc { get; set; }
}
