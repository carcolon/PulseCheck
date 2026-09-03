namespace PulseCheck.Domain.Entities;

public sealed class TransformationalLeaderAssignment
{
    public Guid Id { get; set; }
    public string SolvoId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string OperationsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
