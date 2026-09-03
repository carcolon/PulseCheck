namespace PulseCheck.Domain.Entities;

public sealed class TransformationalLeaderExportJob
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string SolvoId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string OperationsJson { get; set; } = "[]";
    public string Status { get; set; } = "Pending";
    public string FiltersJson { get; set; } = "{}";
    public string? HangfireJobId { get; set; }
    public string? FileName { get; set; }
    public string? FilePath { get; set; }
    public string? Error { get; set; }
    public int ResponseCount { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset? DownloadedAtUtc { get; set; }
    public DateTimeOffset? DismissedAtUtc { get; set; }

    public TransformationalLeaderSession? Session { get; set; }
}
