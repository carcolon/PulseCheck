using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace PulseCheck.Agent;

public sealed class CampaignNotificationListener(
    ILogger<CampaignNotificationListener> logger,
    IOptions<AgentOptions> options,
    PulseCheckApiClient apiClient,
    AgentIdentityResolver identityResolver,
    SyncWakeSignal syncWakeSignal) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var hubUrl = $"{options.Value.BaseUrl.TrimEnd('/')}/hubs/notifications";
        var identity = identityResolver.Resolve(options.Value);

        while (!stoppingToken.IsCancellationRequested)
        {
            var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var connection = new HubConnectionBuilder()
                .WithUrl(hubUrl, hubOptions =>
                {
                    hubOptions.AccessTokenProvider = async () => await apiClient.EnsureAgentTokenAsync(identity, stoppingToken);
                })
                .WithAutomaticReconnect()
                .Build();

            connection.On<object>("campaignCreated", _ =>
            {
                DiagnosticLog.Write("Realtime event received: campaignCreated.");
                syncWakeSignal.Signal();
            });

            connection.On<object>("campaignUpdated", _ =>
            {
                DiagnosticLog.Write("Realtime event received: campaignUpdated.");
                syncWakeSignal.Signal();
            });

            connection.Reconnected += _ =>
            {
                DiagnosticLog.Write("Realtime listener reconnected.");
                syncWakeSignal.Signal();
                return Task.CompletedTask;
            };

            connection.Closed += exception =>
            {
                if (exception is not null)
                {
                    logger.LogWarning(exception, "Realtime listener disconnected.");
                    DiagnosticLog.Write($"Realtime listener disconnected: {exception.Message}");
                }

                disconnected.TrySetResult();
                return Task.CompletedTask;
            };

            try
            {
                await connection.StartAsync(stoppingToken);
                logger.LogInformation("Realtime listener connected to {HubUrl}.", hubUrl);
                DiagnosticLog.Write($"Realtime listener connected to {hubUrl}.");
                syncWakeSignal.Signal();

                await disconnected.Task.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Realtime listener failed. Retrying.");
                DiagnosticLog.Write($"Realtime listener failed: {exception.Message}");
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
