using Microsoft.Extensions.Options;
using PulseCheck.Api.Models;
using PulseCheck.Application.Services;

namespace PulseCheck.Api.Services;

public sealed class EmployeeProfileBackfillHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<PulseCheckOptions> options,
    ILogger<EmployeeProfileBackfillHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var backfillOptions = options.Value.EmployeeProfileBackfill;
        if (!backfillOptions.Enabled)
        {
            logger.LogInformation("Employee profile backfill is disabled.");
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, backfillOptions.StartupDelaySeconds)), stoppingToken);

            using var scope = scopeFactory.CreateScope();
            var backfillService = scope.ServiceProvider.GetRequiredService<EmployeeProfileBackfillService>();
            var result = await backfillService.BackfillAsync(
                backfillOptions.MaxDevicesPerRun,
                backfillOptions.UpdateHistoricalResponses,
                stoppingToken);

            logger.LogInformation(
                "Employee profile backfill finished. CandidateDevices={CandidateDevices}, EnrichedDevices={EnrichedDevices}, UpdatedHistoricalResponses={UpdatedHistoricalResponses}.",
                result.CandidateDevices,
                result.EnrichedDevices,
                result.UpdatedHistoricalResponses);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Employee profile backfill was cancelled because the application is stopping.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Employee profile backfill failed.");
        }
    }
}
