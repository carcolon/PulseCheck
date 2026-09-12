using Microsoft.Extensions.Hosting;

namespace PulseCheck.Agent;

public sealed class InteractiveIdentityReporterService(InteractiveUserIdentityStore identityStore) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await identityStore.ReportCurrentUserAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
