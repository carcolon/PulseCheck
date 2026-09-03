using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PulseCheck.Api.Auth;
using PulseCheck.Application.Common;
using PulseCheck.Application.Services;
using PulseCheck.Api.Reports;

namespace PulseCheck.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = AdminTokenAuthenticationDefaults.Scheme, Roles = "Owner,HRAdmin,WorkforceAdmin")]
[EnableRateLimiting("admin-api")]
[Route("api/dashboard")]
public sealed class DashboardController(PulseCheckService pulseCheckService) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<DashboardOverviewDto>> GetOverview(CancellationToken cancellationToken)
    {
        var overview = await pulseCheckService.GetDashboardOverviewAsync(cancellationToken);
        return Ok(SanitizeOverviewForRole(overview));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummary>> GetSummary(CancellationToken cancellationToken)
    {
        var summary = await pulseCheckService.GetDashboardSummaryAsync(cancellationToken);
        return Ok(summary);
    }

    [HttpGet("report/excel")]
    [Authorize(AuthenticationSchemes = AdminTokenAuthenticationDefaults.Scheme, Roles = "Owner,HRAdmin")]
    public async Task<IActionResult> ExportExcelReport(
        [FromQuery] string range = "weekly",
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] Guid? campaignId = null,
        [FromQuery] string? campaignSearch = null,
        [FromQuery] string? operation = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveDates(range, from, to, out var fromDate, out var toDate, out var error))
        {
            return BadRequest(new { message = error });
        }

        var report = await pulseCheckService.GetReportExportDataAsync(
            fromDate,
            toDate,
            campaignId,
            campaignSearch,
            operation,
            cancellationToken);
        var bytes = PulseCheckExcelReportBuilder.Build(report);
        var fileName = $"PulseCheck-Reporte-{report.FromDate:yyyyMMdd}-{report.ToDate:yyyyMMdd}.xlsx";

        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    private DashboardOverviewDto SanitizeOverviewForRole(DashboardOverviewDto overview)
    {
        if (User.IsInRole("Owner") || (User.IsInRole("HRAdmin") && User.IsInRole("WorkforceAdmin")))
        {
            return overview;
        }

        if (User.IsInRole("HRAdmin"))
        {
            return overview with
            {
                RegisteredDevices = 0,
                PendingAlerts = 0,
                Alerts = [],
                Metrics = overview.Metrics
                    .Where(metric => !string.Equals(metric.Label, "Dispositivos", StringComparison.OrdinalIgnoreCase))
                    .ToArray(),
                NoResponseCount = 0
            };
        }

        return overview with
        {
            HealthTone = "neutral",
            HealthLabel = "Vista operativa",
            HasSignal = false,
            ActiveCampaigns = 0,
            ResponsesToday = 0,
            AverageMood = null,
            PulseDelta = null,
            ParticipationRate = null,
            LatestEvent = "Actividad operativa disponible en agentes y actividad.",
            Alerts = [],
            Metrics = [],
            PulseTrend = [],
            ResponseMix = [],
            ScaleDistribution = [],
            NoResponseCount = 0,
            Actions = [],
            RecentActivity = [],
            Insight = new DashboardInsightDto(
                "attention",
                "Workforce",
                "Monitoreo operativo",
                "Este resumen esta enfocado en agentes, actividad de sesion e inactividad por dispositivo.")
        };
    }

    private static bool TryResolveDates(
        string? range,
        DateOnly? from,
        DateOnly? to,
        out DateOnly? fromDate,
        out DateOnly? toDate,
        out string error)
    {
        error = string.Empty;
        var today = DateOnly.FromDateTime(DateTime.Now);
        var normalized = range?.Trim().ToLowerInvariant() ?? "weekly";

        switch (normalized)
        {
            case "all":
                fromDate = null;
                toDate = null;
                return true;

            case "daily":
                fromDate = today;
                toDate = today;
                return true;

            case "weekly":
                fromDate = today.AddDays(-6);
                toDate = today;
                return true;

            case "custom":
                if (from is null || to is null)
                {
                    fromDate = default;
                    toDate = default;
                    error = "Para range=custom debes enviar from y to (yyyy-MM-dd).";
                    return false;
                }

                fromDate = from.Value;
                toDate = to.Value;
                if (toDate < fromDate)
                {
                    error = "La fecha 'to' no puede ser menor que 'from'.";
                    return false;
                }

                return true;

            default:
                fromDate = default;
                toDate = default;
                error = "range debe ser daily, weekly, custom o all.";
                return false;
        }
    }
}
