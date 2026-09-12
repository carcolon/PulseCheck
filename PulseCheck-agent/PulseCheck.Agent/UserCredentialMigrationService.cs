using Microsoft.Extensions.Hosting;

namespace PulseCheck.Agent;

public sealed class UserCredentialMigrationService(AgentCredentialStore credentialStore) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var token = await credentialStore.ReadTokenAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(token))
            {
                DiagnosticLog.Write("User credential migration check completed.");
            }
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"User credential migration check failed: {exception.Message}");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
