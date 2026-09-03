using System.Text.Json;

namespace PulseCheck.Agent;

public sealed class LocalAgentStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string stateFilePath = Path.Combine(AgentStoragePaths.DataDirectory, "agent-state.json");
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<AgentState> ReadAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(stateFilePath))
            {
                return new AgentState();
            }

            await using var stream = File.OpenRead(stateFilePath);
            var state = await JsonSerializer.DeserializeAsync<AgentState>(stream, SerializerOptions, cancellationToken);
            return state ?? new AgentState();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task WriteAsync(AgentState state, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var stream = File.Create(stateFilePath);
            await JsonSerializer.SerializeAsync(stream, state, SerializerOptions, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }
}
