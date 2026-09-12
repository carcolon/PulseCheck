using System.Text.Json;

namespace PulseCheck.Agent;

public sealed class LocalQueueStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string queueFilePath = Path.Combine(AgentStoragePaths.DataDirectory, "pending-responses.json");
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task EnqueueAsync(PendingResponse response, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var pending = await ReadInternalAsync(cancellationToken);
            pending.Add(response);
            await WriteInternalAsync(pending, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<PendingResponse>> ReadAllAsync(CancellationToken cancellationToken)
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

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await WriteInternalAsync([], cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task ReplaceAsync(IReadOnlyList<PendingResponse> pending, CancellationToken cancellationToken)
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

    private async Task<List<PendingResponse>> ReadInternalAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(queueFilePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(queueFilePath);
        try
        {
            var result = await JsonSerializer.DeserializeAsync<List<PendingResponse>>(stream, SerializerOptions, cancellationToken);
            return result ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task WriteInternalAsync(IReadOnlyList<PendingResponse> pending, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(queueFilePath);
        await JsonSerializer.SerializeAsync(stream, pending, SerializerOptions, cancellationToken);
    }
}
