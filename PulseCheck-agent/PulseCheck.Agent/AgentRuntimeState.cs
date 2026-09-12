namespace PulseCheck.Agent;

public sealed class AgentRuntimeState
{
    private readonly object gate = new();
    private AgentRuntimeSnapshot snapshot = new(
        "Iniciando",
        null,
        null,
        null,
        null,
        null,
        false,
        null);

    public AgentRuntimeSnapshot GetSnapshot()
    {
        lock (gate)
        {
            return snapshot;
        }
    }

    public void MarkStarted(AgentRuntimeIdentity identity)
    {
        lock (gate)
        {
            snapshot = snapshot with
            {
                Status = "En línea",
                DeviceName = identity.Hostname,
                UserName = identity.UserName
            };
        }
    }

    public void MarkSync(DateTimeOffset generatedAtUtc, int activeCampaigns)
    {
        lock (gate)
        {
            snapshot = snapshot with
            {
                Status = "Sincronizado",
                LastSyncAtUtc = generatedAtUtc,
                ActiveCampaigns = activeCampaigns,
                LastError = null
            };
        }
    }

    public void MarkPrompted(string campaignName)
    {
        lock (gate)
        {
            snapshot = snapshot with
            {
                Status = "Esperando respuesta",
                LastPromptedCampaign = campaignName,
                LastError = null
            };
        }
    }

    public void MarkAnswered(string campaignName)
    {
        lock (gate)
        {
            snapshot = snapshot with
            {
                Status = "Respuesta capturada",
                LastPromptedCampaign = campaignName,
                HasPendingResponses = false,
                LastError = null
            };
        }
    }

    public void MarkPendingResponses(bool hasPendingResponses)
    {
        lock (gate)
        {
            snapshot = snapshot with
            {
                HasPendingResponses = hasPendingResponses
            };
        }
    }

    public void MarkIdle()
    {
        lock (gate)
        {
            snapshot = snapshot with
            {
                Status = "En segundo plano",
                LastError = null
            };
        }
    }

    public void MarkError(string message)
    {
        lock (gate)
        {
            snapshot = snapshot with
            {
                Status = "Con incidencias",
                LastError = message
            };
        }
    }
}

public sealed record AgentRuntimeSnapshot(
    string Status,
    string? DeviceName,
    string? UserName,
    DateTimeOffset? LastSyncAtUtc,
    int? ActiveCampaigns,
    string? LastPromptedCampaign,
    bool HasPendingResponses,
    string? LastError = null);
