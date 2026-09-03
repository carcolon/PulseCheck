using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using PulseCheck.Api.Hubs;
using PulseCheck.Api.Reports;
using PulseCheck.Application.Common;
using PulseCheck.Application.Ports;
using PulseCheck.Application.Services;
using PulseCheck.Domain.Abstractions;

namespace PulseCheck.Api.Services;

public sealed class TlExportJobRunner(
    IPulseCheckUnitOfWork unitOfWork,
    TransformationalLeaderDashboardService dashboardService,
    TlExportFileStore fileStore,
    IHubContext<TlNotificationHub> hubContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<TlExportJobRunner> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task RunAsync(Guid exportJobId)
    {
        var job = await unitOfWork.GetTransformationalLeaderExportJobByIdAsync(exportJobId, CancellationToken.None);
        if (job is null)
        {
            return;
        }

        try
        {
            job.Status = "Processing";
            job.UpdatedAtUtc = dateTimeProvider.UtcNow;
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            await NotifyAsync(job);

            var request = JsonSerializer.Deserialize<TlDashboardRequest>(job.FiltersJson, JsonOptions)
                          ?? new TlDashboardRequest([], [], []);
            var session = new TransformationalLeaderSessionDto(
                string.Empty,
                string.Empty,
                DateTimeOffset.MaxValue,
                new AdminUserDto(Guid.Empty, job.Email, job.DisplayName, "TransformationalLeader"),
                job.SolvoId,
                job.Operation,
                TransformationalLeaderOperationScope.Parse(job.OperationsJson, job.Operation));

            var dashboard = await dashboardService.GetDashboardAsync(session, request, CancellationToken.None);
            var bytes = TlResponsesExcelExportBuilder.Build(dashboard);
            var fileName = $"tl-responses-{dateTimeProvider.UtcNow:yyyyMMdd-HHmmss}.xlsx";
            var path = fileStore.CreateExportPath(job.Id, fileName);
            await File.WriteAllBytesAsync(path, bytes, CancellationToken.None);

            job.Status = "Completed";
            job.FileName = fileName;
            job.FilePath = path;
            job.ResponseCount = dashboard.Responses.Count;
            job.CompletedAtUtc = dateTimeProvider.UtcNow;
            job.UpdatedAtUtc = job.CompletedAtUtc.Value;
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            await NotifyAsync(job);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TL export job {ExportJobId} failed.", exportJobId);
            job.Status = "Failed";
            job.Error = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            job.UpdatedAtUtc = dateTimeProvider.UtcNow;
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            await NotifyAsync(job);
        }
    }

    private Task NotifyAsync(PulseCheck.Domain.Entities.TransformationalLeaderExportJob job)
        => hubContext.Clients
            .Group(TlNotificationHub.BuildSessionGroup(job.SessionId))
            .SendAsync("tlExportUpdated", ToDto(job), CancellationToken.None);

    private static TlExportJobDto ToDto(PulseCheck.Domain.Entities.TransformationalLeaderExportJob job)
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
