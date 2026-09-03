namespace PulseCheck.Application.Models;

public sealed class EmailOptions
{
    public string Mode { get; set; } = "AccessKey";
    public string? ConnectionString { get; set; }
    public string? Endpoint { get; set; }
    public string? AccessKey { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string? LogoPath { get; set; }
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
}
