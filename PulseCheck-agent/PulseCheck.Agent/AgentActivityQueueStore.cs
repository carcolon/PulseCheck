using System.Text.Json;

namespace PulseCheck.Agent;

public sealed class AgentActivityQueueStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string queueFilePath = Path.Combine(AgentStoragePaths.MachineDataDirectory, "pending-activity-events.json");
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task EnqueueAsync(AgentActivityEventRequest activityEvent, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var pending = await ReadInternalAsync(cancellationToken);
            pending.Add(activityEvent);
            await WriteInternalAsync(pending, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<AgentActivityEventRequest>> ReadAllAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadInternalAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task ReplaceAsync(IReadOnlyList<AgentActivityEventRequest> pending, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await WriteInternalAsync(pending, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<List<AgentActivityEventRequest>> ReadInternalAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(queueFilePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(queueFilePath);
        try
        {
            var result = await JsonSerializer.DeserializeAsync<List<AgentActivityEventRequest>>(stream, SerializerOptions, cancellationToken);
            return result ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task WriteInternalAsync(IReadOnlyList<AgentActivityEventRequest> pending, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(queueFilePath);
        await JsonSerializer.SerializeAsync(stream, pending, SerializerOptions, cancellationToken);
    }
}
