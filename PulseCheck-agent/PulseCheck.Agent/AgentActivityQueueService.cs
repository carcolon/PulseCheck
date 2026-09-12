namespace PulseCheck.Agent;

public sealed class AgentActivityQueueService(
    ILogger<AgentActivityQueueService> logger,
    PulseCheckApiClient apiClient,
    AgentActivityQueueStore queueStore)
{
    private readonly SemaphoreSlim flushGate = new(1, 1);

    public async Task EnqueueAndFlushAsync(AgentActivityEventRequest activityEvent, CancellationToken cancellationToken)
    {
        await queueStore.EnqueueAsync(activityEvent, cancellationToken);
        await FlushAsync(cancellationToken);
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (!await flushGate.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            var pending = await queueStore.ReadAllAsync(cancellationToken);
            if (pending.Count == 0)
            {
                return;
            }

            var remaining = new List<AgentActivityEventRequest>();
            foreach (var activityEvent in pending)
            {
                try
                {
                    await apiClient.SubmitAgentActivityAsync(activityEvent, cancellationToken);
                    DiagnosticLog.Write($"Activity event submitted. Type={activityEvent.EventType}, Device={activityEvent.DeviceId}, User={activityEvent.UserId}.");
                }
                catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.NotFound)
                {
                    DiagnosticLog.Write($"Dropped queued activity event due to {ex.StatusCode}. Type={activityEvent.EventType}, Device={activityEvent.DeviceId}. Error={ex.Message}");
                    logger.LogWarning(
                        ex,
                        "Dropping queued activity event {EventType} for device {DeviceId} due to non-retriable status {StatusCode}.",
                        activityEvent.EventType,
                        activityEvent.DeviceId,
                        ex.StatusCode);
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Write($"Keeping queued activity event for retry. Type={activityEvent.EventType}, Device={activityEvent.DeviceId}. Error={ex.Message}");
                    logger.LogWarning(
                        ex,
                        "Keeping queued activity event {EventType} for device {DeviceId} to retry later.",
                        activityEvent.EventType,
                        activityEvent.DeviceId);
                    remaining.Add(activityEvent);
                }
            }

            await queueStore.ReplaceAsync(remaining, cancellationToken);
        }
        finally
        {
            flushGate.Release();
        }
    }
}
