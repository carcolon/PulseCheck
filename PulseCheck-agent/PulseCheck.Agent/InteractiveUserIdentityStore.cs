using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PulseCheck.Agent;

public sealed class InteractiveUserIdentityStore(AgentIdentityResolver identityResolver, IOptions<AgentOptions> options)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static string FilePath => Path.Combine(AgentStoragePaths.MachineDataDirectory, "interactive-identity.json");

    public async Task ReportCurrentUserAsync(CancellationToken cancellationToken)
    {
        var identity = identityResolver.Resolve(options.Value, preferInteractiveIdentityStore: false);
        var snapshot = new InteractiveUserIdentitySnapshot(
            identity.UserId,
            identity.UserName,
            identity.Email,
            identity.Department,
            DateTimeOffset.UtcNow);

        await using var stream = File.Create(FilePath);
        await JsonSerializer.SerializeAsync(stream, snapshot, SerializerOptions, cancellationToken);
        DiagnosticLog.Write($"Interactive identity reported for {identity.UserId}.");
    }

    public static InteractiveUserIdentitySnapshot? ReadFresh(TimeSpan maxAge)
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            using var stream = File.OpenRead(FilePath);
            var snapshot = JsonSerializer.Deserialize<InteractiveUserIdentitySnapshot>(stream, SerializerOptions);
            if (snapshot is null || DateTimeOffset.UtcNow - snapshot.ReportedAtUtc > maxAge)
            {
                return null;
            }

            return snapshot;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            DiagnosticLog.Write($"Interactive identity read failed: {exception.Message}");
            return null;
        }
    }
}

public sealed record InteractiveUserIdentitySnapshot(
    string UserId,
    string UserName,
    string Email,
    string Department,
    DateTimeOffset ReportedAtUtc);
