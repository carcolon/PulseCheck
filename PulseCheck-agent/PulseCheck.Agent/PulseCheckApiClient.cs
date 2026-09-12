using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace PulseCheck.Agent;

public sealed class PulseCheckApiClient(
    HttpClient httpClient,
    IOptions<AgentOptions> options,
    AgentCredentialStore credentialStore)
{
    private readonly AgentOptions settings = options.Value;

    public async Task<string> EnsureAgentTokenAsync(AgentRuntimeIdentity identity, CancellationToken cancellationToken)
    {
        var existingToken = await credentialStore.ReadTokenAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(existingToken))
        {
            return existingToken;
        }

        return await RegisterAndStoreAgentTokenAsync(identity, cancellationToken);
    }

    public async Task<AgentSyncResponse> SyncAsync(AgentRuntimeIdentity identity, CancellationToken cancellationToken)
    {
        var endpoint = $"{settings.BaseUrl}/api/agent/sync";
        var request = new RegisterDeviceRequest(
            identity.DeviceId,
            identity.Hostname,
            identity.UserId,
            identity.UserName,
            identity.Email,
            identity.Department,
            identity.OperatingSystem,
            identity.AgentVersion);

        using var response = await PostWithAgentTokenAsync(endpoint, request, identity, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AgentSyncResponse>(cancellationToken: cancellationToken);
        return result ?? throw new InvalidOperationException("Agent sync response was empty.");
    }

    public async Task SubmitResponseAsync(PendingResponse response, CancellationToken cancellationToken)
    {
        var endpoint = $"{settings.BaseUrl}/api/responses";
        using var httpResponse = await PostWithAgentTokenAsync(endpoint, response, AgentRuntimeIdentity.FromResponse(response), cancellationToken);
        if (httpResponse.IsSuccessStatusCode)
        {
            return;
        }

        var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
        var message = $"Response submit failed with {(int)httpResponse.StatusCode} ({httpResponse.StatusCode}). Body: {body}";
        throw new HttpRequestException(message, null, httpResponse.StatusCode);
    }

    public async Task SubmitDeliveryLogAsync(DeliveryLogRequest request, CancellationToken cancellationToken)
    {
        var endpoint = $"{settings.BaseUrl}/api/delivery-logs";
        using var httpResponse = await PostWithAgentTokenAsync(endpoint, request, AgentRuntimeIdentity.FromDeliveryLog(request), cancellationToken);
        httpResponse.EnsureSuccessStatusCode();
    }

    public async Task SubmitAgentActivityAsync(AgentActivityEventRequest request, CancellationToken cancellationToken)
    {
        var endpoint = $"{settings.BaseUrl}/api/agent/activity-events";
        using var httpResponse = await PostWithAgentTokenAsync(endpoint, request, AgentRuntimeIdentity.FromActivity(request), cancellationToken);
        httpResponse.EnsureSuccessStatusCode();
    }

    private async Task<HttpResponseMessage> PostWithAgentTokenAsync<T>(
        string endpoint,
        T payload,
        AgentRuntimeIdentity identity,
        CancellationToken cancellationToken)
    {
        var token = await EnsureAgentTokenAsync(identity, cancellationToken);
        var response = await PostAsJsonWithBearerAsync(endpoint, payload, token, cancellationToken);
        if (response.StatusCode is not (System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden))
        {
            return response;
        }

        response.Dispose();
        token = await RegisterAndStoreAgentTokenAsync(identity, token, cancellationToken);
        return await PostAsJsonWithBearerAsync(endpoint, payload, token, cancellationToken);
    }

    private async Task<string> RegisterAndStoreAgentTokenAsync(
        AgentRuntimeIdentity identity,
        string? currentAgentToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ProvisioningToken))
        {
            throw new InvalidOperationException("Agent provisioning token is not configured.");
        }

        var endpoint = $"{settings.BaseUrl}/api/agent/register";
        var request = new RegisterDeviceRequest(
            identity.DeviceId,
            identity.Hostname,
            identity.UserId,
            identity.UserName,
            identity.Email,
            identity.Department,
            identity.OperatingSystem,
            identity.AgentVersion,
            currentAgentToken);

        using var response = await PostAsJsonWithBearerAsync(endpoint, request, settings.ProvisioningToken, cancellationToken);
        response.EnsureSuccessStatusCode();

        var registration = await response.Content.ReadFromJsonAsync<AgentRegistrationResponse>(cancellationToken: cancellationToken);
        if (registration is null || string.IsNullOrWhiteSpace(registration.AgentToken))
        {
            throw new InvalidOperationException("Agent registration response did not include a token.");
        }

        await credentialStore.SaveTokenAsync(registration.AgentToken, cancellationToken);
        DiagnosticLog.Write("Agent credential was provisioned and stored with Windows data protection.");
        return registration.AgentToken;
    }

    private Task<string> RegisterAndStoreAgentTokenAsync(AgentRuntimeIdentity identity, CancellationToken cancellationToken)
        => RegisterAndStoreAgentTokenAsync(identity, null, cancellationToken);

    private async Task<HttpResponseMessage> PostAsJsonWithBearerAsync<T>(
        string endpoint,
        T payload,
        string bearerToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return await httpClient.SendAsync(request, cancellationToken);
    }
}
