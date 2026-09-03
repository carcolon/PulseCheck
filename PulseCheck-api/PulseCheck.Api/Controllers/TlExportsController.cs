using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PulseCheck.Api.Auth;
using PulseCheck.Api.Services;
using PulseCheck.Application.Common;
using PulseCheck.Application.Ports;
using PulseCheck.Application.Security;
using PulseCheck.Application.Services;
using PulseCheck.Domain.Abstractions;
using PulseCheck.Domain.Entities;

namespace PulseCheck.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = TransformationalLeaderAuthenticationDefaults.Scheme, Roles = "TransformationalLeader")]
[EnableRateLimiting("admin-api")]
[Route("api/tl/exports")]
public sealed class TlExportsController(
    TransformationalLeaderAuthService authService,
    IPulseCheckUnitOfWork unitOfWork,
    IBackgroundJobClient backgroundJobs,
    TlExportFileStore fileStore,
    IDateTimeProvider dateTimeProvider) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [HttpPost]
    public async Task<ActionResult<TlExportJobDto>> CreateExport(
        [FromBody] TlDashboardRequest request,
        CancellationToken cancellationToken)
    {
        var token = ExtractToken();
        var session = await authService.GetSessionAsync(token, cancellationToken);
        var sessionEntity = await ResolveSessionEntityAsync(token, cancellationToken);
        if (session is null || sessionEntity is null)
        {
            return Unauthorized();
        }

        var now = dateTimeProvider.UtcNow;
        var exportJob = new TransformationalLeaderExportJob
        {
            Id = Guid.NewGuid(),
            SessionId = sessionEntity.Id,
            SolvoId = session.SolvoId,
            Email = session.User.Email,
            DisplayName = session.User.DisplayName,
            Operation = session.Operation,
            OperationsJson = System.Text.Json.JsonSerializer.Serialize(session.Operations, JsonOptions),
            Status = "Pending",
            FiltersJson = JsonSerializer.Serialize(NormalizeRequest(request), JsonOptions),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await unitOfWork.AddTransformationalLeaderExportJobAsync(exportJob, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        exportJob.HangfireJobId = backgroundJobs.Enqueue<TlExportJobRunner>(runner => runner.RunAsync(exportJob.Id));
        exportJob.UpdatedAtUtc = dateTimeProvider.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Accepted(ToDto(exportJob));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TlExportJobDto>>> GetExports(CancellationToken cancellationToken)
    {
        var sessionEntity = await ResolveSessionEntityAsync(ExtractToken(), cancellationToken);
        if (sessionEntity is null)
        {
            return Unauthorized();
        }

        var jobs = await unitOfWork.GetVisibleTransformationalLeaderExportJobsBySessionIdAsync(sessionEntity.Id, cancellationToken);
        return Ok(jobs.Select(ToDto).ToArray());
    }

    [HttpPost("{id:guid}/dismiss")]
    public async Task<IActionResult> DismissExport(Guid id, CancellationToken cancellationToken)
    {
        var sessionEntity = await ResolveSessionEntityAsync(ExtractToken(), cancellationToken);
        var job = await unitOfWork.GetTransformationalLeaderExportJobByIdAsync(id, cancellationToken);
        if (sessionEntity is null || job is null || job.SessionId != sessionEntity.Id)
        {
            return NotFound();
        }

        job.DismissedAtUtc = dateTimeProvider.UtcNow;
        job.UpdatedAtUtc = job.DismissedAtUtc.Value;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> DownloadExport(Guid id, CancellationToken cancellationToken)
    {
        var sessionEntity = await ResolveSessionEntityAsync(ExtractToken(), cancellationToken);
        var job = await unitOfWork.GetTransformationalLeaderExportJobByIdAsync(id, cancellationToken);
        if (sessionEntity is null || job is null || job.SessionId != sessionEntity.Id)
        {
            return NotFound();
        }

        if (!string.Equals(job.Status, "Completed", StringComparison.OrdinalIgnoreCase) ||
            !fileStore.Exists(job.FilePath))
        {
            return Conflict(new { message = "El archivo de exportacion aun no esta listo." });
        }

        job.DownloadedAtUtc = dateTimeProvider.UtcNow;
        job.DismissedAtUtc ??= job.DownloadedAtUtc;
        job.UpdatedAtUtc = job.DownloadedAtUtc.Value;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var stream = fileStore.OpenRead(job.FilePath!);
        return File(
            stream,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            job.FileName ?? "tl-responses.xlsx");
    }

    private async Task<TransformationalLeaderSession?> ResolveSessionEntityAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenHash = AdminSecurity.ComputeTokenHash(token);
        var session = await unitOfWork.GetTransformationalLeaderSessionByTokenHashAsync(tokenHash, cancellationToken);
        return session is null || session.RevokedAtUtc is not null || session.ExpiresAtUtc <= dateTimeProvider.UtcNow
            ? null
            : session;
    }

    private string ExtractToken()
    {
        var header = Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : string.Empty;
    }

    private static TlDashboardRequest NormalizeRequest(TlDashboardRequest request)
        => new(
            request.WeekIds?.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            request.CampaignIds?.Where(item => item != Guid.Empty).Distinct().ToArray(),
            request.AnswerFilters?
                .Where(item => item.QuestionId != Guid.Empty && item.Values is { Count: > 0 })
                .Select(item => new TlQuestionAnswerFilterRequest(
                    item.QuestionId,
                    item.Values!.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()))
                .ToArray());

    private static TlExportJobDto ToDto(TransformationalLeaderExportJob job)
        => new(
            job.Id,
            job.Status,
            job.FileName ?? "tl-responses.xlsx",
            job.ResponseCount,
            job.Error,
            job.CreatedAtUtc,
            job.UpdatedAtUtc,
            job.CompletedAtUtc,
            job.DownloadedAtUtc);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
