using System.Net;
using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Options;
using PulseCheck.Api.Models;
using PulseCheck.Application.Models;
using PulseCheck.Application.Ports;

namespace PulseCheck.Api.Services;

public sealed class AcsLeaderAlertEmailService(
    IOptions<EmailOptions> options,
    ILogger<AcsLeaderAlertEmailService> logger) : ILeaderAlertEmailService
{
    private const string InlineLogoContentId = "pulsecheck-logo";
    private readonly EmailOptions emailOptions = options.Value;

    public async Task SendLockedDeviceAlertAsync(LeaderLockAlertEmailRequest request, CancellationToken cancellationToken)
    {
        var recipientAddresses = ResolveRecipientAddresses(request);
        if (!IsConfigured() || recipientAddresses.Length == 0)
        {
            return;
        }

        try
        {
            var client = BuildClient();
            var employeeName = DisplayOrFallback(request.EmployeeName, request.EmployeeEmail);
            var subject = $"PulseCheck inactivity alert: {employeeName}'s PC reached {request.LockedMinutes} minutes locked";
            var content = BuildContent(request, subject);
            var recipients = new EmailRecipients(
                recipientAddresses.Select(item => new EmailAddress(item)).ToArray());
            var message = new EmailMessage(emailOptions.Email!, recipients, content);
            AttachInlineLogoIfAvailable(message);

            await client.SendAsync(WaitUntil.Completed, message, cancellationToken);

            logger.LogInformation(
                "Sent PulseCheck inactivity alert to {RecipientEmails} for employee {EmployeeEmail}.",
                string.Join(", ", recipientAddresses),
                request.EmployeeEmail);
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(
                ex,
                "Failed to send PulseCheck inactivity alert to {RecipientEmails}. Sender {Sender}. Resource {Resource}.",
                string.Join(", ", recipientAddresses),
                emailOptions.Email,
                ResolveConfiguredResource());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send PulseCheck inactivity alert to {RecipientEmails}.", string.Join(", ", recipientAddresses));
        }
    }

    private bool IsConfigured()
    {
        if (string.IsNullOrWhiteSpace(emailOptions.Mode) ||
            !emailOptions.Mode.Equals("AccessKey", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Email mode not supported or not configured. Skipping PulseCheck inactivity alert.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(emailOptions.Email))
        {
            logger.LogWarning("Email sender missing. Skipping PulseCheck inactivity alert.");
            return false;
        }

        var hasConnectionString = !string.IsNullOrWhiteSpace(emailOptions.ConnectionString);
        var hasEndpointKey = !string.IsNullOrWhiteSpace(emailOptions.Endpoint) &&
                             !string.IsNullOrWhiteSpace(emailOptions.AccessKey);

        if (!hasConnectionString && !hasEndpointKey)
        {
            logger.LogWarning("Email settings incomplete. Configure ConnectionString or Endpoint/AccessKey plus Email.");
            return false;
        }

        return true;
    }

    private string[] ResolveRecipientAddresses(LeaderLockAlertEmailRequest request)
        => new[] { request.RecipientEmail }
            .Concat(request.AdditionalRecipientEmails)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private EmailClient BuildClient()
    {
        if (!string.IsNullOrWhiteSpace(emailOptions.ConnectionString))
        {
            return new EmailClient(emailOptions.ConnectionString);
        }

        return new EmailClient(new Uri(emailOptions.Endpoint!), new AzureKeyCredential(emailOptions.AccessKey!));
    }

    private string ResolveConfiguredResource()
    {
        if (!string.IsNullOrWhiteSpace(emailOptions.ConnectionString))
        {
            var endpointPart = emailOptions.ConnectionString
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(part => part.StartsWith("endpoint=", StringComparison.OrdinalIgnoreCase));

            return endpointPart ?? "[connection-string]";
        }

        return emailOptions.Endpoint ?? "[unknown-endpoint]";
    }

    private static EmailContent BuildContent(LeaderLockAlertEmailRequest request, string subject)
    {
        var employeeName = DisplayOrFallback(request.EmployeeName, request.EmployeeEmail);
        var lockReason = FormatLockReason(request.LockReason);
        var idleAtLock = request.IdleSecondsAtLock is int seconds ? FormatDuration(seconds) : "Not available";
        var lockedFor = FormatDuration(request.LockedMinutes * 60);

        var plainText =
            $"""
            PulseCheck inactivity alert

            {employeeName}'s PC reached the {lockedFor} locked-session threshold.

            Employee: {employeeName}
            Employee email: {request.EmployeeEmail}
            Employee ID: {request.EmployeeId}
            Operation: {request.Operation}
            Department: {request.Department}
            Status: {request.EmployeeStatus}
            Lock type: {lockReason}
            Idle time at lock: {idleAtLock}
            Locked at local time: {FormatLocalDateTime(request.LockedAtLocal)}
            Alert sent at local time: {FormatLocalDateTime(request.AlertedAtLocal)}
            """;

        var html =
            $"""
            <!doctype html>
            <html lang="en">
            <body style="margin:0;padding:0;background:#f3f6fb;font-family:Segoe UI,Arial,sans-serif;color:#172033;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f3f6fb;padding:28px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:720px;background:#ffffff;border:1px solid #dce4ef;border-radius:10px;overflow:hidden;">
                      <tr>
                        <td style="padding:24px 28px;background:#ffffff;border-bottom:1px solid #e7edf5;">
                          <table role="presentation" cellspacing="0" cellpadding="0">
                            <tr>
                              <td style="padding-right:12px;">
                                <img src="cid:{InlineLogoContentId}" alt="PulseCheck" width="42" height="42" style="display:block;border:0;width:42px;height:42px;">
                              </td>
                              <td>
                                <div style="font-size:22px;font-weight:800;color:#172033;line-height:1;">PulseCheck</div>
                                <div style="font-size:12px;font-weight:600;color:#64748b;margin-top:4px;">Workforce activity signal</div>
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:28px;background:#3f1d91;color:#ffffff;">
                          <div style="font-size:13px;font-weight:700;text-transform:uppercase;letter-spacing:.08em;color:#d8ccff;margin-bottom:10px;">
                            Inactivity Alert
                          </div>
                          <h1 style="font-size:25px;line-height:1.25;margin:0 0 10px;font-weight:700;">
                            {Html(employeeName)}'s PC reached {request.LockedMinutes} minutes locked
                          </h1>
                          <p style="font-size:15px;line-height:1.55;margin:0;color:#f2edff;">
                            PulseCheck detected an extended locked session that may require leader awareness.
                          </p>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:26px 28px 10px;">
                          <p style="font-size:15px;line-height:1.6;margin:0 0 18px;color:#334155;">
                            Hi {Html(DisplayOrFallback(request.LeaderName, "there"))},
                          </p>
                          <p style="font-size:15px;line-height:1.6;margin:0 0 22px;color:#334155;">
                            The desktop agent reported that this workstation reached the {Html(lockedFor)} locked-session threshold. Please review the context below and follow up if appropriate.
                          </p>
                          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="border-collapse:collapse;border:1px solid #e2e8f0;border-radius:8px;overflow:hidden;">
                            <tr>
                              <td colspan="2" style="padding:14px 16px;background:#f8fafc;border-bottom:1px solid #e2e8f0;font-size:14px;font-weight:700;color:#172033;">
                                Employee and Device Details
                              </td>
                            </tr>
                            {BuildRow("Employee", employeeName)}
                            {BuildRow("Employee email", request.EmployeeEmail)}
                            {BuildRow("Employee ID", request.EmployeeId)}
                            {BuildRow("Operation", request.Operation)}
                            {BuildRow("Department", request.Department)}
                            {BuildRow("Status", request.EmployeeStatus)}
                            {BuildRow("Lock type", lockReason)}
                            {BuildRow("Idle time at lock", idleAtLock)}
                            {BuildRow("Alert threshold", lockedFor)}
                            {BuildRow("Locked at local time", FormatLocalDateTime(request.LockedAtLocal))}
                            {BuildRow("Alert sent at local time", FormatLocalDateTime(request.AlertedAtLocal))}
                          </table>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:18px 28px 28px;">
                          <div style="background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;padding:14px 16px;">
                            <p style="margin:0;font-size:14px;line-height:1.55;color:#7c2d12;">
                              <strong>Recommended next step:</strong> validate whether the user is on break, away from desk, or experiencing a technical issue. This is an operational awareness alert, not an automatic disciplinary escalation.
                            </p>
                          </div>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:18px 28px;background:#f8fafc;border-top:1px solid #e7edf5;">
                          <p style="font-size:12px;line-height:1.5;margin:0;color:#64748b;">
                            This notification was generated by PulseCheck. Please handle this information with confidentiality and discretion.
                          </p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;

        return new EmailContent(subject)
        {
            PlainText = plainText,
            Html = html
        };
    }

    private void AttachInlineLogoIfAvailable(EmailMessage message)
    {
        var logoPath = ResolveLogoPath();
        if (string.IsNullOrWhiteSpace(logoPath) || !File.Exists(logoPath))
        {
            return;
        }

        var bytes = File.ReadAllBytes(logoPath);
        if (bytes.Length == 0)
        {
            return;
        }

        var attachment = new EmailAttachment(
            Path.GetFileName(logoPath),
            ResolveLogoContentType(logoPath),
            BinaryData.FromBytes(bytes))
        {
            ContentId = InlineLogoContentId
        };

        message.Attachments.Add(attachment);
    }

    private string? ResolveLogoPath()
    {
        if (!string.IsNullOrWhiteSpace(emailOptions.LogoPath) && File.Exists(emailOptions.LogoPath))
        {
            return emailOptions.LogoPath;
        }

        var publishedPngLogoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "PulseCheckLogo.png");
        if (File.Exists(publishedPngLogoPath))
        {
            return publishedPngLogoPath;
        }

        var publishedSvgLogoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "PulseCheckLogo.svg");
        return File.Exists(publishedSvgLogoPath) ? publishedSvgLogoPath : null;
    }

    private static string ResolveLogoContentType(string logoPath)
        => string.Equals(Path.GetExtension(logoPath), ".svg", StringComparison.OrdinalIgnoreCase)
            ? "image/svg+xml"
            : "image/png";

    private static string BuildRow(string label, string? value)
    {
        var safeLabel = Html(label);
        var safeValue = Html(string.IsNullOrWhiteSpace(value) ? "N/A" : value.Trim());
        return
            $"""
            <tr>
              <td style="width:190px;padding:12px 16px;border-bottom:1px solid #edf2f7;color:#64748b;font-size:14px;font-weight:600;">{safeLabel}</td>
              <td style="padding:12px 16px;border-bottom:1px solid #edf2f7;color:#172033;font-size:14px;">{safeValue}</td>
            </tr>
            """;
    }

    private static string FormatLockReason(string? lockReason)
        => lockReason?.Trim() switch
        {
            "AutoLock" => "Automatic lock due to inactivity",
            "ManualLock" => "Manual lock",
            _ => string.IsNullOrWhiteSpace(lockReason) ? "Session lock" : lockReason.Trim()
        };

    private static string FormatDuration(int totalSeconds)
    {
        if (totalSeconds < 0)
        {
            totalSeconds = 0;
        }

        var duration = TimeSpan.FromSeconds(totalSeconds);
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours} h {duration.Minutes} min {duration.Seconds} s";
        }

        if (duration.TotalMinutes >= 1)
        {
            return $"{duration.Minutes} min {duration.Seconds} s";
        }

        return $"{duration.Seconds} s";
    }

    private static string FormatLocalDateTime(DateTimeOffset value)
        => value.ToString("yyyy-MM-dd hh:mm:ss tt zzz");

    private static string DisplayOrFallback(string? displayName, string? fallback)
        => !string.IsNullOrWhiteSpace(displayName) ? displayName.Trim() : fallback?.Trim() ?? "Unknown";

    private static string Html(string? value)
        => WebUtility.HtmlEncode(value ?? string.Empty);
}
