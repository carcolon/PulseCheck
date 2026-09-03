using System.Security.Cryptography;
using System.Text;

namespace PulseCheck.Agent;

public sealed class AgentCredentialStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PulseCheck.Agent.DeviceCredential.v1");
    private readonly string credentialFilePath = Path.Combine(AgentStoragePaths.MachineDataDirectory, "agent-credential.dat");
    private readonly string legacyCredentialFilePath = Path.Combine(AgentStoragePaths.DataDirectory, "agent-credential.dat");

    public async Task<string?> ReadTokenAsync(CancellationToken cancellationToken)
    {
        var token = await ReadProtectedTokenAsync(
            credentialFilePath,
            DataProtectionScope.LocalMachine,
            "machine",
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        token = await ReadProtectedTokenAsync(
            legacyCredentialFilePath,
            DataProtectionScope.CurrentUser,
            "legacy user",
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(token))
        {
            await SaveTokenAsync(token, cancellationToken);
            DiagnosticLog.Write("Migrated agent credential from user storage to machine storage.");
        }

        return token;
    }

    public async Task SaveTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Agent token cannot be empty.", nameof(token));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(credentialFilePath)!);
        var tokenBytes = Encoding.UTF8.GetBytes(token);
        var protectedBytes = ProtectedData.Protect(tokenBytes, Entropy, DataProtectionScope.LocalMachine);
        await File.WriteAllBytesAsync(credentialFilePath, protectedBytes, cancellationToken);
    }

    public Task ClearAsync()
    {
        try
        {
            if (File.Exists(credentialFilePath))
            {
                File.Delete(credentialFilePath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DiagnosticLog.Write($"Agent credential clear failed: {exception.Message}");
        }

        return Task.CompletedTask;
    }

    private static async Task<string?> ReadProtectedTokenAsync(
        string path,
        DataProtectionScope scope,
        string storageName,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken);
            var tokenBytes = ProtectedData.Unprotect(protectedBytes, Entropy, scope);
            var token = Encoding.UTF8.GetString(tokenBytes);
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }
        catch (Exception exception) when (exception is CryptographicException or IOException or UnauthorizedAccessException)
        {
            DiagnosticLog.Write($"Agent credential read failed from {storageName} storage: {exception.Message}");
            return null;
        }
    }
}
